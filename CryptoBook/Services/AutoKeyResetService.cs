using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;

namespace CryptoBook.Services;

/// <summary>
/// Координирует таймер, создание lock-снимка, очистку ключа и последующее
/// восстановление. SemaphoreSlim не допускает параллельного reset/restore.
/// </summary>
public sealed class AutoKeyResetService : IKeyResetService
{
    private readonly IKeyProvider keyProvider;
    private readonly ILockSnapshotService snapshotService;
    private readonly IDocumentSession documentSession;
    private readonly IRichTextBoxService richTextBox;
    private readonly IFileTemplateRegistry templates;
    private readonly IWorkspaceFileOpenService fileOpenService;
    private readonly IDispatcherService dispatcher;
    private readonly Application application;
    private readonly SemaphoreSlim transitionGate = new(1, 1);
    private readonly object timerSync = new();
    private Timer? timer;
    private DateTimeOffset lastActivityUtc;
    private int pauseCount;
    private bool started;
    private bool disposed;
    private int failedUnlockAttempts;
    private DateTimeOffset nextUnlockAttemptUtc;

    public AutoKeyResetService(
        IKeyProvider keyProvider,
        ILockSnapshotService snapshotService,
        IDocumentSession documentSession,
        IRichTextBoxService richTextBox,
        IFileTemplateRegistry templates,
        IWorkspaceFileOpenService fileOpenService,
        IDispatcherService dispatcher,
        Application application)
    {
        this.keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        this.snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        this.documentSession = documentSession ?? throw new ArgumentNullException(nameof(documentSession));
        this.richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
        this.templates = templates ?? throw new ArgumentNullException(nameof(templates));
        this.fileOpenService = fileOpenService ?? throw new ArgumentNullException(nameof(fileOpenService));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.application = application ?? throw new ArgumentNullException(nameof(application));

        Timeout = FromSettings(Properties.Settings.Default.KeyResetTimeoutMinutes);
        lastActivityUtc = DateTimeOffset.UtcNow;
    }

    public KeyResetState State { get; private set; } = KeyResetState.Inactive;
    public TimeSpan Timeout { get; private set; }
    public bool IsPaused => Volatile.Read(ref pauseCount) > 0;

    public event EventHandler<KeyResetStateChangedEventArgs>? StateChanged;
    public event EventHandler<Exception>? SnapshotFailed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        lock(timerSync)
        {
            started = true;
            lastActivityUtc = DateTimeOffset.UtcNow;
            timer ??= new Timer(OnTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        RefreshIdleState();
    }

    public void Stop()
    {
        lock(timerSync)
        {
            started = false;
            timer?.Dispose();
            timer = null;
        }
        if(State is KeyResetState.Active or KeyResetState.Inactive)
            SetState(KeyResetState.Inactive);
    }

    public void NotifyActivity()
    {
        lastActivityUtc = DateTimeOffset.UtcNow;
        RefreshIdleState();
    }

    public void UpdateTimeout(TimeSpan timeout)
    {
        if(timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        Timeout = timeout;
        lastActivityUtc = DateTimeOffset.UtcNow;
        RefreshIdleState();
    }

    public IDisposable Pause()
    {
        Interlocked.Increment(ref pauseCount);
        return new PauseScope(this);
    }

    public async Task<bool> ResetAsync(CancellationToken cancellationToken = default)
    {
        if(!keyProvider.HasKey)
        {
            SetState(KeyResetState.Inactive);
            return false;
        }
        if(!await transitionGate.WaitAsync(0, cancellationToken))
            return false;

        try
        {
            SetState(KeyResetState.Resetting);
            if(documentSession.HasDocument)
            {
                var metadata = new LockSnapshotMetadata(
                    documentSession.FilePath,
                    string.IsNullOrWhiteSpace(documentSession.DisplayName)
                        ? "Документ"
                        : documentSession.DisplayName,
                    documentSession.Template?.Id ?? "XamlPackage",
                    documentSession.IsDirty,
                    DateTimeOffset.UtcNow);
                await snapshotService.CreateAndVerifyAsync(richTextBox, metadata, cancellationToken);
            }

            // Ключ очищается только после успешной записи и расшифровки снимка.
            keyProvider.Clear();
            await dispatcher.InvokeAsync(() =>
            {
                documentSession.Close();
                foreach(Window window in application.Windows.Cast<Window>().ToArray())
                {
                    if(window is not CryptoBook.Views.MainWindow)
                        window.Close();
                }
            });
            SetState(KeyResetState.KeyReset);
            return true;
        }
        catch(Exception exception) when(exception is not OperationCanceledException)
        {
            // До этой точки Clear не выполнялся: документ и ключ остаются доступны.
            lastActivityUtc = DateTimeOffset.UtcNow;
            SetState(KeyResetState.Active);
            SnapshotFailed?.Invoke(this, exception);
            return false;
        }
        finally
        {
            transitionGate.Release();
        }
    }

    public async Task<bool> TryUnlockAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        if(DateTimeOffset.UtcNow < nextUnlockAttemptUtc)
            return false;
        if(!await transitionGate.WaitAsync(0, cancellationToken))
            return false;

        char[] characters = key.ToCharArray();
        try
        {
            SetState(KeyResetState.Unlocking);
            keyProvider.Clear();
            keyProvider.SetKey(characters);
            if(snapshotService.Exists)
                _ = await snapshotService.ReadAndVerifyAsync(cancellationToken);
            failedUnlockAttempts = 0;
            nextUnlockAttemptUtc = DateTimeOffset.MinValue;
            lastActivityUtc = DateTimeOffset.UtcNow;
            SetState(KeyResetState.Active);
            return true;
        }
        catch(Exception exception) when(exception is not OperationCanceledException)
        {
            keyProvider.Clear();
            failedUnlockAttempts = Math.Min(failedUnlockAttempts + 1, 6);
            nextUnlockAttemptUtc = DateTimeOffset.UtcNow +
                TimeSpan.FromMilliseconds(500 * Math.Pow(2, failedUnlockAttempts - 1));
            SetState(KeyResetState.KeyReset);
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
            transitionGate.Release();
        }
    }

    public async Task RestoreSnapshotAsync(
        bool restoreAsUnsaved,
        CancellationToken cancellationToken = default)
    {
        if(!keyProvider.HasKey)
            throw new InvalidOperationException("Ключ не задан.");
        await transitionGate.WaitAsync(cancellationToken);
        try
        {
            SetState(KeyResetState.Restoring);
            (var document, var metadata) = await snapshotService.ReadAndVerifyAsync(cancellationToken);

            if(!restoreAsUnsaved &&
               !string.IsNullOrWhiteSpace(metadata.OriginalPath) &&
               File.Exists(metadata.OriginalPath))
            {
                WorkspaceFileOpenResult result = await fileOpenService.OpenAsync(metadata.OriginalPath, cancellationToken);
                if(!result.Success)
                    throw new IOException(result.Error ?? "Не удалось открыть исходный файл.");
            }
            else
            {
                IFileTemplate template = templates.GetById(metadata.TemplateId)
                    ?? templates.GetAll().First(item => item.OpenMode == FileOpenMode.Document);
                string sessionPath = !string.IsNullOrWhiteSpace(metadata.OriginalPath)
                    ? metadata.OriginalPath
                    : Path.Combine(Path.GetTempPath(), metadata.DocumentName);
                await dispatcher.InvokeAsync(() =>
                {
                    documentSession.Open(sessionPath, template, document);
                    documentSession.SetDisplayName(metadata.DocumentName);
                    documentSession.MarkDirty();
                });
            }

            snapshotService.Delete();
            lastActivityUtc = DateTimeOffset.UtcNow;
            SetState(KeyResetState.Active);
        }
        finally
        {
            transitionGate.Release();
        }
    }

    public void Dispose()
    {
        if(disposed)
            return;
        disposed = true;
        Stop();
        transitionGate.Dispose();
    }

    private void OnTimer(object? state)
    {
        if(!started || IsPaused || Timeout == TimeSpan.Zero)
            return;
        if(!keyProvider.HasKey)
        {
            if(State == KeyResetState.Active)
                SetState(KeyResetState.Inactive);
            return;
        }
        if(State == KeyResetState.Inactive)
            SetState(KeyResetState.Active);
        if(State != KeyResetState.Active)
            return;
        if(DateTimeOffset.UtcNow - lastActivityUtc >= Timeout)
            _ = ResetAsync();
    }

    private void Resume()
    {
        int value = Interlocked.Decrement(ref pauseCount);
        if(value < 0)
        {
            Interlocked.Exchange(ref pauseCount, 0);
            return;
        }
        if(value == 0)
            NotifyActivity();
    }

    private void RefreshIdleState()
    {
        if(State is KeyResetState.Resetting or KeyResetState.KeyReset or KeyResetState.Unlocking or KeyResetState.Restoring)
            return;
        SetState(started && Timeout > TimeSpan.Zero && keyProvider.HasKey
            ? KeyResetState.Active
            : KeyResetState.Inactive);
    }

    private void SetState(KeyResetState state)
    {
        if(State == state)
            return;
        State = state;
        void Raise() => StateChanged?.Invoke(this, new KeyResetStateChangedEventArgs(state));
        if(dispatcher.CheckAccess()) Raise(); else dispatcher.BeginInvoke(Raise);
    }

    private static TimeSpan FromSettings(int minutes) =>
        minutes <= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);

    private sealed class PauseScope(AutoKeyResetService owner) : IDisposable
    {
        private AutoKeyResetService? owner = owner;
        public void Dispose() => Interlocked.Exchange(ref owner, null)?.Resume();
    }
}

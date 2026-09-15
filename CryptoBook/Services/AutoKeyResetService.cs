using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;

using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using WpfApplication = System.Windows.Application;
using ThreadingTimer = System.Threading.Timer;

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
    private readonly Lazy<IWorkspaceFileOpenService> fileOpenService;
    private readonly IDispatcherService dispatcher;
    private readonly IDocumentRecoveryService? recoveryService;
    private readonly SemaphoreSlim transitionGate = new(1, 1);
    private readonly object timerSync = new();
    private ThreadingTimer? timer;
    private DateTimeOffset lastActivityUtc;
    private int pauseCount;
    private bool started;
    private bool disposed;
    private int failedUnlockAttempts;
    private DateTimeOffset nextUnlockAttemptUtc;
    private readonly PasswordProtectedBuffer unlockProtection;
    private byte[]? unlockVerifier;
    private bool documentRetainedAfterFailure;

    public AutoKeyResetService(
        IKeyProvider keyProvider,
        ILockSnapshotService snapshotService,
        IDocumentSession documentSession,
        IRichTextBoxService richTextBox,
        IFileTemplateRegistry templates,
        Lazy<IWorkspaceFileOpenService> fileOpenService,
        IDispatcherService dispatcher,
        WpfApplication application,
        IDocumentRecoveryService? recoveryService = null)
    {
        this.keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        unlockProtection = new PasswordProtectedBuffer(keyProvider);
        this.snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        this.documentSession = documentSession ?? throw new ArgumentNullException(nameof(documentSession));
        this.richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
        this.templates = templates ?? throw new ArgumentNullException(nameof(templates));
        this.fileOpenService = fileOpenService ?? throw new ArgumentNullException(nameof(fileOpenService));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        // Kept in the public constructor for existing hosts; WindowBehavior owns windows.
        ArgumentNullException.ThrowIfNull(application);
        this.recoveryService = recoveryService;

        Timeout = FromSettings(Properties.Settings.Default.KeyResetTimeoutMinutes);
        lastActivityUtc = DateTimeOffset.UtcNow;
    }

    // Совместимый конструктор для хостов и тестов, создающих сервис вручную.
    // В контейнере используется ленивый вариант, чтобы разорвать цикл
    // WorkspaceFileOpenService -> MenuFileModel -> AutoKeyResetService.
    public AutoKeyResetService(
        IKeyProvider keyProvider,
        ILockSnapshotService snapshotService,
        IDocumentSession documentSession,
        IRichTextBoxService richTextBox,
        IFileTemplateRegistry templates,
        IWorkspaceFileOpenService fileOpenService,
        IDispatcherService dispatcher,
        WpfApplication application)
        : this(
            keyProvider,
            snapshotService,
            documentSession,
            richTextBox,
            templates,
            new Lazy<IWorkspaceFileOpenService>(() => fileOpenService),
            dispatcher,
            application)
    {
    }

    public KeyResetState State { get; private set; } = KeyResetState.Inactive;
    public TimeSpan Timeout { get; private set; }
    public bool IsPaused => Volatile.Read(ref pauseCount) > 0;
    public bool HasRetainedDocument => documentRetainedAfterFailure;

    public event EventHandler<KeyResetStateChangedEventArgs>? StateChanged;
    public event EventHandler<Exception>? SnapshotFailed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if(!keyProvider.HasKey && snapshotService.Exists)
            SetState(KeyResetState.KeyReset);
        lock(timerSync)
        {
            started = true;
            lastActivityUtc = DateTimeOffset.UtcNow;
            if(keyProvider.HasKey)
                EnsureTimer();
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
        if(started && keyProvider.HasKey)
        {
            lock(timerSync)
                EnsureTimer();
        }
        RefreshIdleState();
    }

    public void UpdateTimeout(TimeSpan timeout)
    {
        if(timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        Timeout = timeout;
        lastActivityUtc = DateTimeOffset.UtcNow;
        if(started && keyProvider.HasKey && timeout > TimeSpan.Zero)
        {
            lock(timerSync)
                EnsureTimer();
        }
        else if(timeout == TimeSpan.Zero)
        {
            lock(timerSync)
            {
                timer?.Dispose();
                timer = null;
            }
        }
        RefreshIdleState();
    }

    public IDisposable Pause()
    {
        Interlocked.Increment(ref pauseCount);
        return new PauseScope(this);
    }

    public async Task<bool> ResetAsync(CancellationToken cancellationToken = default)
    {
        if(!dispatcher.CheckAccess())
        {
            await Task.Yield();
            return await await dispatcher.InvokeAsync(() => ResetAsync(cancellationToken));
        }
        if(State is KeyResetState.Resetting or KeyResetState.Unlocking ||
           (State == KeyResetState.KeyReset && documentRetainedAfterFailure))
            return false;
        if(!keyProvider.HasKey)
        {
            SetState(KeyResetState.Inactive);
            return false;
        }
        if(!await transitionGate.WaitAsync(0, cancellationToken))
            return false;

        WorkspaceDocumentSnapshot? inactive = null;
        bool hasProtectedDocument = true;
        try
        {
            var workspace = documentSession as IWorkspaceDocumentSession;
            inactive = workspace?.CaptureInactiveDocument();
            bool inactiveProtected = inactive?.TemplateId == "Encrypted file";
            hasProtectedDocument = documentSession.Template is SecureFileTemplate || inactiveProtected;
            if(!hasProtectedDocument)
            {
                // A cached encryption password is not an application login.
                keyProvider.Clear();
                documentRetainedAfterFailure = false;
                SetState(KeyResetState.Inactive);
                return true;
            }

            SetState(KeyResetState.Resetting);
            // A separate password verifier survives snapshot I/O failure or deletion.
            // It contains no document data and cannot be opened using Windows credentials.
            if(unlockVerifier is not null)
                CryptographicOperations.ZeroMemory(unlockVerifier);
            unlockVerifier = null;
            unlockVerifier = await unlockProtection.ProtectAsync("CryptoBook unlock"u8.ToArray(), CancellationToken.None);
            if(recoveryService is not null)
                await recoveryService.StopAsync();
            documentRetainedAfterFailure = false;
            bool protectBothDocuments = documentSession.Template is SecureFileTemplate && inactiveProtected;
            if(documentSession.Template is not SecureFileTemplate && inactiveProtected)
            {
                // Park the ordinary document and snapshot only the encrypted editor.
                if(workspace?.SelectPage(inactive!.IsMarkdown ? "MarkdownEditor" : "Home") != true)
                    throw new InvalidOperationException("Не удалось защитить неактивный документ.");
            }
            if(documentSession.HasDocument)
            {
                var metadata = new LockSnapshotMetadata(
                    documentSession.FilePath,
                    string.IsNullOrWhiteSpace(documentSession.DisplayName)
                        ? "Документ"
                        : documentSession.DisplayName,
                    documentSession.Template?.Id ?? "XamlPackage",
                    documentSession.IsDirty,
                    DateTimeOffset.UtcNow)
                {
                    InactiveDocument = protectBothDocuments ? inactive : null
                };
                await snapshotService.CreateAndVerifyAsync(richTextBox, metadata, cancellationToken);
                if(recoveryService is not null)
                    await recoveryService.DeleteSnapshotAsync();
            }

            // Ключ очищается только после успешной записи и расшифровки снимка.
            keyProvider.Clear();
            await dispatcher.InvokeAsync(() =>
            {
                if(!protectBothDocuments && workspace is not null)
                    workspace.CloseCurrent();
                else
                    documentSession.Close();
            });
            SetState(KeyResetState.KeyReset);
            recoveryService?.Start();
            return true;
        }
        catch(Exception exception)
        {
            // Preserve unsaved documents in the hidden workspace, but revoke the key.
            // If verifier creation itself failed, remain locked; retrying must not accept
            // an arbitrary password merely because no snapshot was written.
            documentRetainedAfterFailure = hasProtectedDocument && documentSession.HasDocument;
            keyProvider.Clear();
            SetState(KeyResetState.KeyReset);
            SnapshotFailed?.Invoke(this, exception);
            return false;
        }
        finally
        {
            if(inactive is not null)
                CryptographicOperations.ZeroMemory(inactive.Content);
            transitionGate.Release();
        }
    }

    public async Task<bool> TryUnlockAsync(ReadOnlyMemory<char> key, CancellationToken cancellationToken = default)
    {
        if(key.Length == 0)
            return false;
        if(DateTimeOffset.UtcNow < nextUnlockAttemptUtc)
            return false;
        if(!await transitionGate.WaitAsync(0, cancellationToken))
            return false;

        if(State is KeyResetState.Resetting or KeyResetState.Unlocking or KeyResetState.Restoring ||
           (!documentRetainedAfterFailure && !snapshotService.Exists))
        {
            transitionGate.Release();
            return false;
        }
        char[] characters = GC.AllocateArray<char>(key.Length, pinned: true);
        key.Span.CopyTo(characters);
        try
        {
            SetState(KeyResetState.Unlocking);
            keyProvider.Clear();
            keyProvider.SetKey(characters);
            if(!documentRetainedAfterFailure && snapshotService.Exists)
                _ = await snapshotService.ReadAndVerifyAsync(cancellationToken);
            else if(unlockVerifier is not null)
            {
                byte[] verified = await unlockProtection.UnprotectAsync(unlockVerifier, cancellationToken);
                CryptographicOperations.ZeroMemory(verified);
            }
            else
                throw new CryptographicException("Нет доступного подтверждения ключа.");
            failedUnlockAttempts = 0;
            nextUnlockAttemptUtc = DateTimeOffset.MinValue;
            lastActivityUtc = DateTimeOffset.UtcNow;
            documentRetainedAfterFailure = false;
            SetState(KeyResetState.Active);
            recoveryService?.Start();
            if(unlockVerifier is not null)
                CryptographicOperations.ZeroMemory(unlockVerifier);
            unlockVerifier = null;
            return true;
        }
        catch(OperationCanceledException)
        {
            keyProvider.Clear();
            SetState(KeyResetState.KeyReset);
            throw;
        }
        catch(Exception)
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
                WorkspaceFileOpenResult result = await fileOpenService.Value.OpenAsync(metadata.OriginalPath, cancellationToken);
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

            if(documentSession is IWorkspaceDocumentSession workspace)
                await dispatcher.InvokeAsync(() => workspace.RestoreInactiveDocument(metadata.InactiveDocument));
            snapshotService.Delete();
            lastActivityUtc = DateTimeOffset.UtcNow;
            SetState(KeyResetState.Active);
        }
        catch
        {
            lastActivityUtc = DateTimeOffset.UtcNow;
            SetState(keyProvider.HasKey ? KeyResetState.Active : KeyResetState.KeyReset);
            throw;
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
        if(unlockVerifier is not null)
            CryptographicOperations.ZeroMemory(unlockVerifier);
        keyProvider.Clear();
        transitionGate.Dispose();
    }

    private void OnTimer(object? state)
    {
        if(!started || IsPaused || Timeout == TimeSpan.Zero)
            return;
        if(!keyProvider.HasKey)
        {
            lock(timerSync)
            {
                timer?.Dispose();
                timer = null;
            }
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

    private void EnsureTimer()
    {
        timer ??= new ThreadingTimer(
            OnTimer,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
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
        if(State is KeyResetState.Resetting or KeyResetState.Unlocking or KeyResetState.Restoring ||
           (State == KeyResetState.KeyReset && (documentRetainedAfterFailure || !keyProvider.HasKey)))
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

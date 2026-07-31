using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace CryptoBook.Services
{
    public sealed class DocumentRecoveryService: IDocumentRecoveryService
    {
        private static readonly byte[] AdditionalEntropy =
            Encoding.UTF8.GetBytes("CryptoBook.DocumentRecovery.v1");
        private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan FailureLogInterval =
            TimeSpan.FromMinutes(5);

        private readonly IDocumentSession documentSession;
        private readonly IRichTextBoxService richTextBox;
        private readonly IFlowDocumentSaveService saveService;
        private readonly IFlowDocumentLoadService loadService;
        private readonly IFileTemplateRegistry templateRegistry;
        private readonly DispatcherTimer timer;
        private readonly string recoveryFilePath;
        private readonly Action<Exception> autosaveFailureLogger;
        private readonly Func<DateTimeOffset> getUtcNow;
        private readonly XamlPackageFileTemplate recoveryTemplate = new();
        private bool started;
        private bool saving;
        private bool disposed;
        private long lastSnapshotRevision = -1;
        private long invalidationVersion;
        private DateTimeOffset? lastAutosaveFailureLoggedAt;

        public DocumentRecoveryService(
            IDocumentSession documentSession,
            IRichTextBoxService richTextBox,
            IFlowDocumentSaveService saveService,
            IFlowDocumentLoadService loadService,
            IFileTemplateRegistry templateRegistry,
            Dispatcher dispatcher)
            : this(
                documentSession,
                richTextBox,
                saveService,
                loadService,
                templateRegistry,
                dispatcher,
                GetDefaultRecoveryFilePath())
        {
        }

        internal DocumentRecoveryService(
            IDocumentSession documentSession,
            IRichTextBoxService richTextBox,
            IFlowDocumentSaveService saveService,
            IFlowDocumentLoadService loadService,
            IFileTemplateRegistry templateRegistry,
            Dispatcher dispatcher,
            string recoveryFilePath,
            Action<Exception>? autosaveFailureLogger = null,
            Func<DateTimeOffset>? getUtcNow = null)
        {
            this.documentSession = documentSession
                ?? throw new ArgumentNullException(nameof(documentSession));
            this.richTextBox = richTextBox
                ?? throw new ArgumentNullException(nameof(richTextBox));
            this.saveService = saveService
                ?? throw new ArgumentNullException(nameof(saveService));
            this.loadService = loadService
                ?? throw new ArgumentNullException(nameof(loadService));
            this.templateRegistry = templateRegistry
                ?? throw new ArgumentNullException(nameof(templateRegistry));
            ArgumentNullException.ThrowIfNull(dispatcher);
            ArgumentException.ThrowIfNullOrWhiteSpace(recoveryFilePath);

            this.recoveryFilePath = Path.GetFullPath(recoveryFilePath);
            this.autosaveFailureLogger =
                autosaveFailureLogger ?? WriteAutosaveFailureToLog;
            this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
            DeleteStaleTemporaryFiles(this.recoveryFilePath);
            timer = new DispatcherTimer(
                SaveDelay,
                DispatcherPriority.ApplicationIdle,
                OnTimerTick,
                dispatcher);
            timer.Stop();
        }

        public bool HasSnapshot => File.Exists(recoveryFilePath);

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if(started)
                return;

            started = true;
            documentSession.PropertyChanged += OnSessionPropertyChanged;
            ScheduleIfDirty();
        }

        public async Task<bool> RestoreSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if(!HasSnapshot)
                return false;

            byte[] encrypted = await File.ReadAllBytesAsync(
                recoveryFilePath,
                cancellationToken);
            byte[] plaintext = ProtectedData.Unprotect(
                encrypted,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);

            try
            {
                RecoveryEnvelope envelope =
                    JsonSerializer.Deserialize<RecoveryEnvelope>(plaintext)
                    ?? throw new InvalidDataException(
                        "Файл восстановления не содержит данных.");
                if(envelope.Version != 1)
                    throw new InvalidDataException(
                        "Версия файла восстановления не поддерживается.");
                byte[] documentBytes = Convert.FromBase64String(
                    envelope.Document);
                try
                {
                    await using MemoryStream source =
                        new(documentBytes, writable: false);
                    await loadService.LoadAsync(
                        richTextBox,
                        source,
                        recoveryTemplate,
                        cancellationToken);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(documentBytes);
                }

                IFileTemplate? originalTemplate =
                    templateRegistry.GetById(
                        envelope.TemplateId ?? string.Empty);
                if(!string.IsNullOrWhiteSpace(envelope.FilePath) &&
                   originalTemplate is not null)
                {
                    documentSession.Open(
                        envelope.FilePath,
                        originalTemplate);
                }
                else
                {
                    documentSession.SetDisplayName(
                        string.IsNullOrWhiteSpace(envelope.DisplayName)
                            ? "Восстановленный документ.XamlPackage"
                            : envelope.DisplayName);
                }

                documentSession.MarkDirty();
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        public Task DeleteSnapshotAsync()
        {
            timer.Stop();
            invalidationVersion++;
            lastSnapshotRevision = -1;
            File.Delete(recoveryFilePath);
            return Task.CompletedTask;
        }

        internal Task SaveSnapshotNowAsync() => SaveSnapshotAsync();

        private void OnSessionPropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs args)
        {
            if(args.PropertyName is nameof(IDocumentSession.IsDirty) or
               nameof(IDocumentSession.Revision) or
               nameof(IDocumentSession.SavedRevision))
                ScheduleIfDirty();
        }

        private void ScheduleIfDirty()
        {
            if(!started ||
               saving ||
               !documentSession.IsDirty ||
               documentSession.Revision == lastSnapshotRevision)
                return;

            if(!timer.IsEnabled)
                timer.Start();
        }

        private async void OnTimerTick(object? sender, EventArgs args)
        {
            timer.Stop();
            if(saving || !documentSession.IsDirty)
                return;

            saving = true;
            try
            {
                await SaveSnapshotAsync();
                lastAutosaveFailureLoggedAt = null;
            }
            catch(Exception exception)
            {
                LogAutosaveFailure(exception);
            }
            finally
            {
                saving = false;
                if(documentSession.IsDirty &&
                   documentSession.Revision != lastSnapshotRevision)
                    timer.Start();
            }
        }

        internal void LogAutosaveFailure(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            DateTimeOffset now = getUtcNow();
            if(lastAutosaveFailureLoggedAt is DateTimeOffset lastLoggedAt &&
               now - lastLoggedAt < FailureLogInterval)
                return;

            lastAutosaveFailureLoggedAt = now;
            try
            {
                autosaveFailureLogger(exception);
            }
            catch(Exception loggingException)
            {
                Trace.TraceError(
                    "Не удалось записать ошибку автосохранения: {0}",
                    loggingException);
            }
        }

        private async Task SaveSnapshotAsync()
        {
            long revision = documentSession.Revision;
            long snapshotInvalidationVersion = invalidationVersion;
            await using MemoryStream document = new();
            await saveService.SaveToStreamAsync(
                richTextBox,
                document,
                recoveryTemplate);

            RecoveryEnvelope envelope = new(
                1,
                documentSession.FilePath,
                documentSession.DisplayName,
                documentSession.Template?.Id,
                revision,
                DateTimeOffset.UtcNow,
                Convert.ToBase64String(document.ToArray()));
            byte[] serialized =
                JsonSerializer.SerializeToUtf8Bytes(envelope);
            byte[] encrypted;
            try
            {
                encrypted = ProtectedData.Protect(
                    serialized,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(serialized);
            }

            string? directory = Path.GetDirectoryName(recoveryFilePath);
            if(string.IsNullOrWhiteSpace(directory))
                throw new IOException(
                    "Не удалось определить каталог восстановления.");
            Directory.CreateDirectory(directory);
            string temporaryPath =
                recoveryFilePath + "." +
                Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                await using(FileStream output = new(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await output.WriteAsync(encrypted);
                    await output.FlushAsync();
                    output.Flush(flushToDisk: true);
                }

                if(snapshotInvalidationVersion != invalidationVersion ||
                   !documentSession.IsDirty)
                    return;

                File.Move(
                    temporaryPath,
                    recoveryFilePath,
                    overwrite: true);
                lastSnapshotRevision = revision;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encrypted);
                TryDelete(temporaryPath);
            }
        }

        private static string GetDefaultRecoveryFilePath() =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "CryptoBook",
                "Recovery",
                "current.recovery");

        private static void WriteAutosaveFailureToLog(
            Exception exception)
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "CryptoBook",
                "Logs",
                "recovery.log");
            string? directory = Path.GetDirectoryName(logPath);
            if(string.IsNullOrWhiteSpace(directory))
                throw new IOException(
                    "Не удалось определить каталог журнала.");

            Directory.CreateDirectory(directory);
            File.AppendAllText(
                logPath,
                $"{DateTimeOffset.Now:O} Ошибка автосохранения:" +
                $"{Environment.NewLine}{exception}" +
                $"{Environment.NewLine}{Environment.NewLine}",
                Encoding.UTF8);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if(File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void DeleteStaleTemporaryFiles(
            string recoveryPath)
        {
            try
            {
                string? directory =
                    Path.GetDirectoryName(recoveryPath);
                if(string.IsNullOrWhiteSpace(directory) ||
                   !Directory.Exists(directory))
                    return;

                string pattern =
                    Path.GetFileName(recoveryPath) + ".*.tmp";
                foreach(string path in
                    Directory.EnumerateFiles(directory, pattern))
                    TryDelete(path);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            timer.Stop();
            documentSession.PropertyChanged -= OnSessionPropertyChanged;
        }

        private sealed record RecoveryEnvelope(
            int Version,
            string? FilePath,
            string DisplayName,
            string? TemplateId,
            long Revision,
            DateTimeOffset SavedAt,
            string Document);
    }
}

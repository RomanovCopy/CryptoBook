using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace CryptoBook.Services
{
    /// <summary>
    /// Создаёт отложенный снимок несохранённого документа и защищает его средствами
    /// DPAPI текущего пользователя. Снимок не заменяет обычное сохранение документа.
    /// </summary>
    public sealed class DocumentRecoveryService: IDocumentRecoveryService
    {
        private static readonly byte[] AdditionalEntropy =
            Encoding.UTF8.GetBytes("CryptoBook.DocumentRecovery.v1");
        private static readonly byte[] RecoveryMagic =
            Encoding.ASCII.GetBytes("CBRECV02");
        private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan FailureLogInterval =
            TimeSpan.FromMinutes(5);
        private static readonly TimeSpan[] DeleteRetryDelays =
        [
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(150),
            TimeSpan.FromMilliseconds(300)
        ];

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
        private readonly SemaphoreSlim recoveryFileGate = new(1, 1);
        private readonly object saveTaskSync = new();
        private Task activeSaveTask = Task.CompletedTask;
        private bool started;
        private bool stopping;
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

            stopping = false;
            started = true;
            documentSession.PropertyChanged += OnSessionPropertyChanged;
            ScheduleIfDirty();
        }

        public async Task StopAsync()
        {
            if(disposed || stopping)
                return;

            stopping = true;
            started = false;
            timer.Stop();
            documentSession.PropertyChanged -= OnSessionPropertyChanged;
            invalidationVersion++;
            lastSnapshotRevision = -1;
            await WaitForActiveSaveAsync();
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
                await using var binarySource = new MemoryStream(
                    plaintext,
                    writable: false);
                RecoveryMetadata? metadata = await BinarySnapshotEnvelope
                    .TryReadHeaderAsync<RecoveryMetadata>(
                        binarySource,
                        RecoveryMagic,
                        cancellationToken);
                if(metadata is not null)
                {
                    await using Stream documentSource =
                        BinarySnapshotEnvelope.OpenPayloadStream(binarySource);
                    await loadService.LoadAsync(
                        richTextBox,
                        documentSource,
                        recoveryTemplate,
                        cancellationToken);
                }
                else
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

                    metadata = new RecoveryMetadata(
                        envelope.FilePath,
                        envelope.DisplayName,
                        envelope.TemplateId,
                        envelope.Revision,
                        envelope.SavedAt);
                }

                IFileTemplate? originalTemplate =
                    templateRegistry.GetById(
                        metadata.TemplateId ?? string.Empty);
                if(!string.IsNullOrWhiteSpace(metadata.FilePath) &&
                   originalTemplate is not null)
                {
                    documentSession.Open(
                        metadata.FilePath,
                        originalTemplate);
                }
                else
                {
                    documentSession.SetDisplayName(
                        string.IsNullOrWhiteSpace(metadata.DisplayName)
                            ? "Восстановленный документ.XamlPackage"
                            : metadata.DisplayName);
                }

                documentSession.MarkDirty();
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        public async Task DeleteSnapshotAsync()
        {
            timer.Stop();
            invalidationVersion++;
            lastSnapshotRevision = -1;
            await WaitForActiveSaveAsync();

            await recoveryFileGate.WaitAsync();
            try
            {
                await DeleteRecoveryFileWithRetryAsync();
            }
            finally
            {
                recoveryFileGate.Release();
            }
        }

        internal Task SaveSnapshotNowAsync() => GetOrStartSaveTask();

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
               stopping ||
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
            if(stopping || saving || !documentSession.IsDirty)
                return;

            saving = true;
            try
            {
                await GetOrStartSaveTask();
                lastAutosaveFailureLoggedAt = null;
            }
            catch(Exception exception)
            {
                LogAutosaveFailure(exception);
            }
            finally
            {
                saving = false;
                if(!stopping &&
                   documentSession.IsDirty &&
                   documentSession.Revision != lastSnapshotRevision)
                    timer.Start();
            }
        }

        private Task GetOrStartSaveTask()
        {
            // Таймер и принудительное сохранение могут сработать одновременно.
            // Все вызывающие ожидают одну общую задачу, а не пишут файл параллельно.
            lock(saveTaskSync)
            {
                if(activeSaveTask.IsCompleted)
                    activeSaveTask = SaveSnapshotAsync();
                return activeSaveTask;
            }
        }

        private async Task WaitForActiveSaveAsync()
        {
            Task saveTask;
            lock(saveTaskSync)
                saveTask = activeSaveTask;

            try
            {
                await saveTask;
            }
            catch(Exception exception)
            {
                // Очистка должна продолжиться даже после сбоя автосохранения.
                LogAutosaveFailure(exception);
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
            // Версия инвалидируется при остановке и удалении снимка. Она не позволяет
            // уже начатому сохранению воскресить файл восстановления после очистки.
            long snapshotInvalidationVersion = invalidationVersion;
            RecoveryMetadata metadata = new(
                documentSession.FilePath,
                documentSession.DisplayName,
                documentSession.Template?.Id,
                revision,
                DateTimeOffset.UtcNow);
            await using MemoryStream document = new();
            await saveService.SaveToStreamAsync(
                richTextBox,
                document,
                recoveryTemplate);
            await using MemoryStream envelope = new();
            await BinarySnapshotEnvelope.WriteHeaderAsync(
                envelope,
                RecoveryMagic,
                metadata);
            document.Position = 0;
            await document.CopyToAsync(envelope);
            byte[] serialized = envelope.ToArray();
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

                // Проверяем актуальность непосредственно перед атомарной публикацией,
                // удерживая тот же семафор, которым защищено удаление снимка.
                await recoveryFileGate.WaitAsync();
                try
                {
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
                    recoveryFileGate.Release();
                }
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

        private async Task DeleteRecoveryFileWithRetryAsync()
        {
            for(int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Delete(recoveryFilePath);
                    return;
                }
                catch(FileNotFoundException)
                {
                    return;
                }
                catch(DirectoryNotFoundException)
                {
                    // Отсутствующий каталог означает, что очищать уже нечего.
                    return;
                }
                catch(Exception exception) when(
                    exception is IOException or UnauthorizedAccessException &&
                    attempt < DeleteRetryDelays.Length)
                {
                    // Краткая блокировка файла не должна мешать штатному закрытию.
                    await Task.Delay(DeleteRetryDelays[attempt]);
                }
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
            stopping = true;
            started = false;
            timer.Stop();
            documentSession.PropertyChanged -= OnSessionPropertyChanged;
        }

        private sealed record RecoveryMetadata(
            string? FilePath,
            string DisplayName,
            string? TemplateId,
            long Revision,
            DateTimeOffset SavedAt);

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

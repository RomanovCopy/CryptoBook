using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Views;

using CryptoBook.Infrastructure;

using System.Diagnostics;
using System.IO;

namespace CryptoBook.Services
{
    /// <summary>
    /// Координирует безопасное переключение документа и выбирает внутреннее
    /// или системное открытие. Защищённые файлы расшифровываются только во
    /// временный каталог текущего процесса.
    /// </summary>
    public sealed class WorkspaceFileOpenService:
        IWorkspaceFileOpenService,
        IDocumentSwitchCoordinator,
        IDisposable
    {
        private readonly SemaphoreSlim switchGate = new(1, 1);
        private readonly ISecureFileValidator secureFileValidator;
        private readonly ISecureFileProcessor secureFileProcessor;
        private readonly IEncryptionKeyRequestService keyRequestService;
        private readonly IWindowManager windowManager;
        private readonly IProgressDialogService progressDialogService;
        private readonly IFileLauncherService fileLauncherService;
        private readonly IFileTemplateRegistry fileTemplateRegistry;
        private readonly IWorkspaceInternalFileOpenService internalFileOpenService;
        private readonly IUnsavedChangesGuard unsavedChangesGuard;
        private readonly IDocumentSession documentSession;
        private readonly IDocumentRecoveryService recoveryService;
        private readonly IDocumentDialogService dialogService;
        private readonly IKeyResetService? keyResetService;
        private readonly IRecentDocumentService? recentDocumentService;
        private readonly string temporaryRoot;
        private bool disposed;

        public WorkspaceFileOpenService(
            ISecureFileValidator secureFileValidator,
            ISecureFileProcessor secureFileProcessor,
            IEncryptionKeyRequestService keyRequestService,
            IWindowManager windowManager,
            IProgressDialogService progressDialogService,
            IFileLauncherService fileLauncherService,
            IFileTemplateRegistry fileTemplateRegistry,
            IWorkspaceInternalFileOpenService internalFileOpenService,
            IUnsavedChangesGuard unsavedChangesGuard,
            IDocumentSession documentSession,
            IDocumentRecoveryService recoveryService,
            IDocumentDialogService dialogService,
            IKeyResetService? keyResetService = null,
            IRecentDocumentService? recentDocumentService = null)
        {
            this.secureFileValidator = secureFileValidator ??
                throw new ArgumentNullException(nameof(secureFileValidator));
            this.secureFileProcessor = secureFileProcessor ??
                throw new ArgumentNullException(nameof(secureFileProcessor));
            this.keyRequestService = keyRequestService ??
                throw new ArgumentNullException(nameof(keyRequestService));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            this.progressDialogService = progressDialogService ??
                throw new ArgumentNullException(nameof(progressDialogService));
            this.fileLauncherService = fileLauncherService ??
                throw new ArgumentNullException(nameof(fileLauncherService));
            this.fileTemplateRegistry = fileTemplateRegistry ??
                throw new ArgumentNullException(nameof(fileTemplateRegistry));
            this.internalFileOpenService = internalFileOpenService ??
                throw new ArgumentNullException(nameof(internalFileOpenService));
            this.unsavedChangesGuard = unsavedChangesGuard ??
                throw new ArgumentNullException(nameof(unsavedChangesGuard));
            this.documentSession = documentSession ??
                throw new ArgumentNullException(nameof(documentSession));
            this.recoveryService = recoveryService ??
                throw new ArgumentNullException(nameof(recoveryService));
            this.dialogService = dialogService ??
                throw new ArgumentNullException(nameof(dialogService));
            this.keyResetService = keyResetService;
            this.recentDocumentService = recentDocumentService;

            string externalRoot = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook",
                "External");
            CleanupOrphanedDirectories(externalRoot);
            temporaryRoot = Path.Combine(
                externalRoot,
                $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        }

        public Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            OpenAsync(filePath, null, cancellationToken);

        public Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            MediaCatalogSelection? mediaCatalog,
            CancellationToken cancellationToken = default) =>
            SwitchAsync(
                filePath,
                requestEncryptionKey: false,
                mediaCatalog,
                cancellationToken);

        public async Task<WorkspaceFileOpenResult> OpenWithAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if(keyResetService?.State is KeyResetState.Resetting)
                return WorkspaceFileOpenResult.Fail("Выполняется безопасный сброс ключа.");
            using IDisposable? timerPause = keyResetService?.Pause();
            if(string.IsNullOrWhiteSpace(filePath))
                return WorkspaceFileOpenResult.Fail("File path is empty.");

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(
                    GetLocalNativePath(filePath));
            }
            catch(Exception exception) when(
                exception is ArgumentException or NotSupportedException)
            {
                return WorkspaceFileOpenResult.Fail(exception.Message);
            }

            await switchGate.WaitAsync(cancellationToken);
            try
            {
                string? accessError = ValidateReadableFile(normalizedPath);
                if(accessError is not null)
                    return WorkspaceFileOpenResult.Fail(accessError);

                bool isEncrypted = await secureFileValidator
                    .HasCryptoBookHeaderAsync(
                        normalizedPath,
                        cancellationToken);
                if(!isEncrypted)
                    return LaunchOpenWith(normalizedPath);

                if(!keyRequestService.EnsureKeyAvailable())
                    return WorkspaceFileOpenResult.Cancel();

                (string operationDirectory, string decryptedPath) =
                    await CreateDecryptedTemporaryCopyAsync(
                        normalizedPath,
                        cancellationToken);
                try
                {
                    string protectedCopyPath = RenameDecryptedCopyForDisplay(
                        normalizedPath,
                        decryptedPath);
                    File.SetAttributes(
                        protectedCopyPath,
                        File.GetAttributes(protectedCopyPath) |
                            FileAttributes.ReadOnly);

                    WorkspaceFileOpenResult result =
                        LaunchOpenWith(protectedCopyPath);
                    if(!result.Success)
                        TryDeleteDirectory(operationDirectory);
                    return result;
                }
                catch
                {
                    TryDeleteDirectory(operationDirectory);
                    throw;
                }
            }
            finally
            {
                switchGate.Release();
            }
        }

        public Task<WorkspaceFileOpenResult> OpenFromShellAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            SwitchAsync(
                filePath,
                requestEncryptionKey: true,
                mediaCatalog: null,
                cancellationToken);

        public async Task<WorkspaceFileOpenResult> SwitchAsync(
            string targetPath,
            CancellationToken cancellationToken = default) =>
            await SwitchAsync(
                targetPath,
                requestEncryptionKey: false,
                mediaCatalog: null,
                cancellationToken);

        private async Task<WorkspaceFileOpenResult> SwitchAsync(
            string targetPath,
            bool requestEncryptionKey,
            MediaCatalogSelection? mediaCatalog,
            CancellationToken cancellationToken)
        {
            // В состоянии Restoring сервис сам вызывает штатное открытие
            // исходного файла; блокируем только внешний Resetting.
            if(keyResetService?.State is KeyResetState.Resetting)
                return WorkspaceFileOpenResult.Fail("Выполняется безопасный сброс ключа.");
            using IDisposable? timerPause = keyResetService?.Pause();
            if(string.IsNullOrWhiteSpace(targetPath))
                return WorkspaceFileOpenResult.Fail("File path is empty.");

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(
                    GetLocalNativePath(targetPath));
            }
            catch(Exception exception) when(
                exception is ArgumentException or NotSupportedException)
            {
                return WorkspaceFileOpenResult.Fail(exception.Message);
            }

            await switchGate.WaitAsync(cancellationToken);
            try
            {
                bool isCurrentDocument = IsCurrentDocument(normalizedPath);
                bool? knownEncryptionState = null;
                if(isCurrentDocument)
                {
                    if(!requestEncryptionKey)
                        return WorkspaceFileOpenResult.InternalSuccess();

                    knownEncryptionState = await secureFileValidator
                        .HasCryptoBookHeaderAsync(
                            normalizedPath,
                            cancellationToken);
                    if(!knownEncryptionState.Value)
                        return WorkspaceFileOpenResult.InternalSuccess();
                }

                string? accessError = ValidateReadableFile(normalizedPath);
                if(accessError is not null)
                    return WorkspaceFileOpenResult.Fail(accessError);

                if(!await unsavedChangesGuard.CanProceedAsync(
                    cancellationToken))
                {
                    return WorkspaceFileOpenResult.Cancel();
                }

                WorkspaceFileOpenResult result = await OpenCoreAsync(
                    normalizedPath,
                    requestEncryptionKey,
                    knownEncryptionState,
                    mediaCatalog,
                    cancellationToken);
                if(result.Success && IsCurrentDocument(normalizedPath))
                    await TryDeletePreviousRecoverySnapshotAsync();
                if(result.Success)
                    await TryRecordOpenedAsync(normalizedPath);
                return result;
            }
            finally
            {
                switchGate.Release();
            }
        }

        private async Task<WorkspaceFileOpenResult> OpenCoreAsync(
            string filePath,
            bool requestEncryptionKey,
            bool? knownEncryptionState,
            MediaCatalogSelection? mediaCatalog,
            CancellationToken cancellationToken)
        {
            bool isEncrypted = knownEncryptionState ??
                await secureFileValidator.HasCryptoBookHeaderAsync(
                    filePath,
                    cancellationToken);
            if(!isEncrypted)
            {
                IFileTemplate? template = FindTemplate(filePath);
                if(template?.OpenMode == FileOpenMode.Document)
                {
                    await internalFileOpenService.OpenDocumentAsync(
                        filePath,
                        filePath,
                        template,
                        sourceIsEncrypted: false,
                        cancellationToken);
                    return WorkspaceFileOpenResult.InternalSuccess();
                }

                if(template?.OpenMode == FileOpenMode.Media)
                    return OpenMedia(filePath, mediaCatalog);

                LaunchResult launchResult = fileLauncherService.Open(filePath);
                return launchResult.Success
                    ? WorkspaceFileOpenResult.ExternalSuccess()
                    : WorkspaceFileOpenResult.Fail(launchResult.Error);
            }

            bool keyAvailable = requestEncryptionKey
                ? keyRequestService.RequestKey()
                : keyRequestService.EnsureKeyAvailable();
            if(!keyAvailable)
                return WorkspaceFileOpenResult.Cancel();

            (string operationDirectory, string decryptedPath) =
                await CreateDecryptedTemporaryCopyAsync(
                    filePath,
                    cancellationToken);

            try
            {
                IFileTemplate? template = FindTemplate(decryptedPath);
                if(template?.OpenMode == FileOpenMode.Document)
                {
                    await internalFileOpenService.OpenDocumentAsync(
                        filePath,
                        decryptedPath,
                        template,
                        sourceIsEncrypted: true,
                        cancellationToken);
                    TryDeleteDirectory(operationDirectory);
                    return WorkspaceFileOpenResult.InternalSuccess();
                }

                if(template?.OpenMode == FileOpenMode.Media)
                {
                    // Медиаплеер читает файл после возврата из метода, поэтому каталог
                    // остаётся жить до Dispose сервиса вместе с окном приложения.
                    return OpenMedia(decryptedPath, mediaCatalog);
                }

                LaunchResult launchResult = fileLauncherService.Open(decryptedPath);
                if(!launchResult.Success)
                {
                    TryDeleteDirectory(operationDirectory);
                    return WorkspaceFileOpenResult.Fail(launchResult.Error);
                }

                return WorkspaceFileOpenResult.ExternalSuccess();
            }
            catch
            {
                TryDeleteDirectory(operationDirectory);
                throw;
            }
        }

        private async Task<(string OperationDirectory, string DecryptedPath)>
            CreateDecryptedTemporaryCopyAsync(
                string encryptedPath,
                CancellationToken cancellationToken)
        {
            // Каждый открытый файл изолирован в отдельном каталоге, чтобы исходный
            // контейнер никогда не передавался стороннему приложению.
            string operationDirectory = Path.Combine(
                temporaryRoot,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(operationDirectory);

            try
            {
                string outputBasePath = Path.Combine(
                    operationDirectory,
                    "document");
                await progressDialogService.RunAsync(
                    LocalizationManager.GetString("Media.DecryptingFile"),
                    async (progress, token) =>
                    {
                        using var linkedTokenSource =
                            CancellationTokenSource.CreateLinkedTokenSource(
                                cancellationToken,
                                token);
                        await secureFileProcessor.DecryptFileAsyncToFile(
                            encryptedPath,
                            outputBasePath,
                            progress,
                            linkedTokenSource.Token);
                        return true;
                    });

                string[] outputFiles = Directory.GetFiles(operationDirectory);
                if(outputFiles.Length != 1)
                {
                    throw new IOException(
                        LocalizationManager.GetString(
                            "Media.DecryptedFileUnknown"));
                }

                return (operationDirectory, outputFiles[0]);
            }
            catch
            {
                TryDeleteDirectory(operationDirectory);
                throw;
            }
        }

        private WorkspaceFileOpenResult LaunchOpenWith(string path)
        {
            LaunchResult launchResult =
                fileLauncherService.ShowOpenWithDialog(path);
            return launchResult.Success
                ? WorkspaceFileOpenResult.ExternalSuccess()
                : WorkspaceFileOpenResult.Fail(launchResult.Error);
        }

        private static string RenameDecryptedCopyForDisplay(
            string encryptedPath,
            string decryptedPath)
        {
            string baseName = Path.GetFileNameWithoutExtension(encryptedPath);
            if(string.IsNullOrWhiteSpace(baseName))
                return decryptedPath;

            string displayPath = Path.Combine(
                Path.GetDirectoryName(decryptedPath)!,
                baseName + Path.GetExtension(decryptedPath));
            if(string.Equals(
                displayPath,
                decryptedPath,
                StringComparison.OrdinalIgnoreCase))
            {
                return decryptedPath;
            }

            File.Move(decryptedPath, displayPath);
            return displayPath;
        }

        private WorkspaceFileOpenResult OpenMedia(
            string filePath,
            MediaCatalogSelection? mediaCatalog)
        {
            var context = new Dictionary<string, object?>
            {
                ["path"] = filePath
            };
            if(mediaCatalog is not null && mediaCatalog.FilePaths.Count > 0)
            {
                context[MediaCatalogSelection.WindowContextKey] =
                    mediaCatalog;
            }
            // FileExplorer и другие окна-источники могут закрыться
            // сразу после открытия. MediaPlayer должен принадлежать их
            // владельцу, а не самому временному окну.
            Guid mediaWindowId =
                windowManager.CreateSiblingWindow<MediaPlayer>(context);
            windowManager.ShowWindow(mediaWindowId);
            return WorkspaceFileOpenResult.InternalSuccess();
        }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            TryDeleteDirectory(temporaryRoot);
            switchGate.Dispose();
        }

        private bool IsCurrentDocument(string path) =>
            !string.IsNullOrWhiteSpace(documentSession.FilePath) &&
            string.Equals(
                Path.GetFullPath(documentSession.FilePath),
                path,
                StringComparison.OrdinalIgnoreCase);

        private static string GetLocalNativePath(string path)
        {
            const string localPrefix = "local://";
            return path.StartsWith(
                localPrefix,
                StringComparison.OrdinalIgnoreCase)
                ? path[localPrefix.Length..]
                : path;
        }

        private static string? ValidateReadableFile(string path)
        {
            try
            {
                using FileStream _ = new(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                return null;
            }
            catch(Exception exception) when(
                exception is IOException or UnauthorizedAccessException)
            {
                return exception.Message;
            }
        }

        private async Task TryDeletePreviousRecoverySnapshotAsync()
        {
            try
            {
                await recoveryService.DeleteSnapshotAsync();
            }
            catch(Exception exception)
            {
                dialogService.ShowRecoveryCleanupError(exception);
            }
        }

        private async Task TryRecordOpenedAsync(string path)
        {
            if(recentDocumentService is null)
                return;

            try
            {
                // Открытие уже завершено; сбой необязательной истории не должен
                // превращать успешное открытие документа в ошибку.
                await recentDocumentService.RecordOpenedAsync(
                    path,
                    CancellationToken.None);
            }
            catch(Exception exception)
            {
                Debug.WriteLine(exception);
            }
        }

        private IFileTemplate? FindTemplate(string path) =>
            fileTemplateRegistry.GetAll()
                .FirstOrDefault(item => item.CanHandleExtension(
                    Path.GetExtension(path)));

        private static void CleanupOrphanedDirectories(string externalRoot)
        {
            if(!Directory.Exists(externalRoot))
                return;

            // Имя каталога начинается с PID владельца. Каталоги живых процессов
            // не трогаем: параллельно могут быть запущены несколько экземпляров.
            foreach(string directory in Directory.GetDirectories(externalRoot))
            {
                string name = Path.GetFileName(directory);
                int separator = name.IndexOf('-');
                if(separator <= 0 ||
                   !int.TryParse(name.AsSpan(0, separator), out int processId) ||
                   IsProcessRunning(processId))
                {
                    continue;
                }

                TryDeleteDirectory(directory);
            }
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch(ArgumentException)
            {
                return false;
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if(!Directory.Exists(path))
                    return;

                foreach(string file in Directory.EnumerateFiles(
                    path,
                    "*",
                    SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(
                            file,
                            File.GetAttributes(file) &
                                ~FileAttributes.ReadOnly);
                    }
                    catch(IOException)
                    {
                    }
                    catch(UnauthorizedAccessException)
                    {
                    }
                }

                Directory.Delete(path, recursive: true);
            }
            catch(IOException)
            {
                // Внешнее приложение может удерживать расшифрованный файл.
            }
            catch(UnauthorizedAccessException)
            {
            }
        }
    }
}

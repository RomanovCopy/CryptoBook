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
            SwitchAsync(filePath, cancellationToken);

        public async Task<WorkspaceFileOpenResult> SwitchAsync(
            string targetPath,
            CancellationToken cancellationToken = default)
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
                if(IsCurrentDocument(normalizedPath))
                    return WorkspaceFileOpenResult.InternalSuccess();

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
            CancellationToken cancellationToken)
        {
            bool isEncrypted = await secureFileValidator
                .HasCryptoBookHeaderAsync(filePath, cancellationToken);
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
                    return OpenMedia(filePath);

                LaunchResult launchResult = fileLauncherService.Open(filePath);
                return launchResult.Success
                    ? WorkspaceFileOpenResult.ExternalSuccess()
                    : WorkspaceFileOpenResult.Fail(launchResult.Error);
            }

            if(!keyRequestService.EnsureKeyAvailable())
                return WorkspaceFileOpenResult.Cancel();

            // Отдельный каталог на операцию упрощает проверку результата и позволяет
            // удалить открытый текст целиком после внутреннего открытия или ошибки.
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
                            filePath,
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

                string decryptedPath = outputFiles[0];
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
                    return OpenMedia(decryptedPath);
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

        private WorkspaceFileOpenResult OpenMedia(string filePath)
        {
            var context = new Dictionary<string, object?>
            {
                ["path"] = filePath
            };
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
                if(Directory.Exists(path))
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

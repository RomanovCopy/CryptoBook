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
    /// Выбирает внутреннее или системное открытие файла. Защищённые файлы
    /// расшифровываются только во временный каталог текущего процесса.
    /// </summary>
    public sealed class WorkspaceFileOpenService:
        IWorkspaceFileOpenService,
        IDisposable
    {
        private readonly ISecureFileValidator secureFileValidator;
        private readonly ISecureFileProcessor secureFileProcessor;
        private readonly IEncryptionKeyRequestService keyRequestService;
        private readonly IWindowManager windowManager;
        private readonly IProgressDialogService progressDialogService;
        private readonly IFileLauncherService fileLauncherService;
        private readonly IFileTemplateRegistry fileTemplateRegistry;
        private readonly IWorkspaceInternalFileOpenService internalFileOpenService;
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
            IWorkspaceInternalFileOpenService internalFileOpenService)
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

            string externalRoot = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook",
                "External");
            CleanupOrphanedDirectories(externalRoot);
            temporaryRoot = Path.Combine(
                externalRoot,
                $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        }

        public async Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(filePath))
                return WorkspaceFileOpenResult.Fail("File path is empty.");

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
                    var context = new Dictionary<string, object?>
                    {
                        ["path"] = decryptedPath
                    };
                    Guid mediaWindowId = windowManager.CreateWindow<MediaPlayer>(
                        context);
                    windowManager.ShowWindow(mediaWindowId);
                    return WorkspaceFileOpenResult.InternalSuccess();
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

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            TryDeleteDirectory(temporaryRoot);
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

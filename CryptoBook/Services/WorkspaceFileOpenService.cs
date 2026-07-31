using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Views;

using System.Diagnostics;
using System.IO;

namespace CryptoBook.Services
{
    public sealed class WorkspaceFileOpenService:
        IWorkspaceFileOpenService,
        IDisposable
    {
        private readonly ISecureFileValidator secureFileValidator;
        private readonly ISecureFileProcessor secureFileProcessor;
        private readonly IKeyProvider keyProvider;
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
            IKeyProvider keyProvider,
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
            this.keyProvider = keyProvider ??
                throw new ArgumentNullException(nameof(keyProvider));
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
                LaunchResult launchResult = fileLauncherService.Open(filePath);
                return launchResult.Success
                    ? WorkspaceFileOpenResult.ExternalSuccess()
                    : WorkspaceFileOpenResult.Fail(launchResult.Error);
            }

            if(!EnsureEncryptionKey())
                return WorkspaceFileOpenResult.Cancel();

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
                    "Расшифровка файла",
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
                        "Не удалось определить расшифрованный файл.");
                }

                string decryptedPath = outputFiles[0];
                IFileTemplate? template = fileTemplateRegistry.GetAll()
                    .FirstOrDefault(item => item.CanHandleExtension(
                        Path.GetExtension(decryptedPath)));
                if(template?.OpenMode == FileOpenMode.Document)
                {
                    await internalFileOpenService.OpenDocumentAsync(
                        filePath,
                        decryptedPath,
                        template,
                        cancellationToken);
                    TryDeleteDirectory(operationDirectory);
                    return WorkspaceFileOpenResult.InternalSuccess();
                }

                if(template?.OpenMode == FileOpenMode.Media)
                {
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

        private bool EnsureEncryptionKey()
        {
            if(keyProvider.HasKey)
                return true;

            // Защищённый результат поиска открывается только после ввода ключа.
            Guid keyWindowId = windowManager.CreateWindow<KeyInputWindow>();
            windowManager.ShowWindowDialog(keyWindowId);
            return keyProvider.HasKey;
        }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            TryDeleteDirectory(temporaryRoot);
        }

        private static void CleanupOrphanedDirectories(string externalRoot)
        {
            if(!Directory.Exists(externalRoot))
                return;

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

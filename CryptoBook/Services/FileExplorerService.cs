using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Views;

namespace CryptoBook.Services
{
    /// <summary>
    /// Открывает FileExplorer как файловый менеджер или как встроенный диалог выбора.
    /// Системный проводник в этих сценариях не используется.
    /// </summary>
    public sealed class FileExplorerService:
        IFileExplorerService,
        IFilePickerService,
        IFolderPickerService
    {
        public const string ModeContextKey = "fileExplorerMode";
        public const string InitialDirectoryContextKey = "initialDirectory";
        public const string FileSelectionHandlerContextKey =
            "fileSelectionHandler";

        private readonly IWindowManager windowManager;
        private readonly IFileManagerService fileManagerService;

        public FileExplorerService(
            IWindowManager windowManager,
            IFileManagerService fileManagerService)
        {
            this.windowManager = windowManager
                ?? throw new ArgumentNullException(nameof(windowManager));
            this.fileManagerService = fileManagerService
                ?? throw new ArgumentNullException(nameof(fileManagerService));
        }

        public void Show(string? initialDirectory = null)
        {
            Guid windowId = windowManager.CreateWindow<FileExplorer>(
                CreateContext(FileExplorerMode.Manage, initialDirectory));
            windowManager.ShowWindow(windowId);
        }

        public void ShowFileSelection(
            string? initialDirectory,
            Action<MediaCatalogSelection> fileSelected)
        {
            ArgumentNullException.ThrowIfNull(fileSelected);
            Guid windowId = windowManager.CreateSiblingWindow<FileExplorer>(
                CreateContext(
                    FileExplorerMode.SelectFile,
                    initialDirectory,
                    fileSelected));
            // MediaPlayer использует FileExplorer как постоянный браузер:
            // каждый выбор открывает файл, не завершая окно выбора.
            windowManager.ShowWindow(windowId);
        }

        public Task<string?> PickFileAsync(
            string? initialDirectory,
            CancellationToken ct) =>
            PickAsync(FileExplorerMode.SelectFile, initialDirectory, ct);

        public Task<string?> PickFolderAsync(
            string? initialDirectory,
            CancellationToken ct) =>
            PickAsync(FileExplorerMode.SelectFolder, initialDirectory, ct);

        private Task<string?> PickAsync(
            FileExplorerMode mode,
            string? initialDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Guid windowId = windowManager.CreateWindow<FileExplorer>(
                CreateContext(mode, initialDirectory));
            windowManager.ShowWindowDialog(windowId);
            string? result = windowManager.GetResult<string>(windowId);
            cancellationToken.ThrowIfCancellationRequested();
            if(string.IsNullOrWhiteSpace(result))
                return Task.FromResult<string?>(null);

            string normalizedPath = fileManagerService.NormalizePath(result);
            const string localPrefix = "local://";
            string pickerPath = normalizedPath.StartsWith(
                localPrefix,
                StringComparison.OrdinalIgnoreCase)
                ? normalizedPath[localPrefix.Length..]
                : normalizedPath;
            return Task.FromResult<string?>(pickerPath);
        }

        private static IReadOnlyDictionary<string, object?> CreateContext(
            FileExplorerMode mode,
            string? initialDirectory,
            Action<MediaCatalogSelection>? fileSelected = null)
        {
            var context = new Dictionary<string, object?>
            {
                [ModeContextKey] = mode
            };
            if(!string.IsNullOrWhiteSpace(initialDirectory))
                context[InitialDirectoryContextKey] = initialDirectory;
            if(fileSelected is not null)
                context[FileSelectionHandlerContextKey] = fileSelected;
            return context;
        }
    }
}

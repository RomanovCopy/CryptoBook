using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    /// <summary>
    /// Совместимый локальный адаптер к единому FileExplorer.
    /// </summary>
    public sealed class LocalFolderPickerService: IFolderPickerService
    {
        private readonly FileExplorerService fileExplorerService;

        public LocalFolderPickerService(FileExplorerService fileExplorerService) =>
            this.fileExplorerService = fileExplorerService
                ?? throw new ArgumentNullException(nameof(fileExplorerService));

        public Task<string?> PickFolderAsync(string? initialDirectory, CancellationToken ct) =>
            fileExplorerService.PickFolderAsync(initialDirectory, ct);
    }
}

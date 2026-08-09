using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    /// <summary>
    /// Совместимый адаптер старого контракта к единому FileExplorer.
    /// </summary>
    public sealed class FolderPickerService: IFolderPickerService
    {
        private readonly FileExplorerService fileExplorerService;

        public FolderPickerService(FileExplorerService fileExplorerService) =>
            this.fileExplorerService = fileExplorerService
                ?? throw new ArgumentNullException(nameof(fileExplorerService));

        public Task<string?> PickFolderAsync(string? initialDirectory, CancellationToken ct) =>
            fileExplorerService.PickFolderAsync(initialDirectory, ct);
    }
}

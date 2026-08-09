using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    /// <summary>
    /// Совместимый адаптер старого контракта к единому FileExplorer.
    /// </summary>
    public sealed class FilePickerService:IFilePickerService
    {
        private readonly FileExplorerService fileExplorerService;

        public FilePickerService(FileExplorerService fileExplorerService) =>
            this.fileExplorerService = fileExplorerService
                ?? throw new ArgumentNullException(nameof(fileExplorerService));

        public Task<string?> PickFileAsync(string? initialDirectory, CancellationToken ct) =>
            fileExplorerService.PickFileAsync(initialDirectory, ct);
    }
}

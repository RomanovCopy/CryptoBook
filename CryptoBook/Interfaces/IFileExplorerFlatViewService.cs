using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IFileExplorerFlatViewService: IService, IDisposable
{
    event EventHandler? FilesChanged;

    Task<FileExplorerFlatScanResult> ScanAsync(
        string rootPath,
        bool includeHidden,
        CancellationToken cancellationToken = default);

    void StartMonitoring(string rootPath);
    void StopMonitoring();
}

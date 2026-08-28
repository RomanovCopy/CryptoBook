using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface ITransferEngine: IService
{
    Task<FileOperationResult> CopyAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);

    Task<FileOperationResult> MoveAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
}

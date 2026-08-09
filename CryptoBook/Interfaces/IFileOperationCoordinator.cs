using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IFileOperationCoordinator: IService
    {
        Task<FileOperationBatchResult> TransferAsync(
            IEnumerable<string> sourcePaths,
            string destinationDirectory,
            FileTransferKind operation,
            CancellationToken cancellationToken = default);

        Task<FileOperationBatchResult> DeleteAsync(
            IEnumerable<string> sourcePaths,
            CancellationToken cancellationToken = default);
    }
}

using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IFileOperationCoordinator: IService
    {
        Task<FileOperationBatchResult> RestoreAsync(string directory,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        Task<FileOperationBatchResult> TransferAsync(
            IEnumerable<string> sourcePaths,
            string destinationDirectory,
            FileTransferKind operation,
            CancellationToken cancellationToken = default,
            Func<Task>? synchronizeViewAsync = null);

        Task<FileOperationBatchResult> DeleteAsync(
            IEnumerable<string> sourcePaths,
            CancellationToken cancellationToken = default,
            Func<Task>? synchronizeViewAsync = null);
    }
}

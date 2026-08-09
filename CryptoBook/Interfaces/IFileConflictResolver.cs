using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IFileConflictResolver: IService
    {
        Task<FileConflictDecision> ResolveAsync(
            string sourcePath,
            string destinationPath,
            bool isDirectory,
            CancellationToken cancellationToken);
    }
}

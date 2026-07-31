using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IWorkspaceFileOpenService
    {
        Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default);
    }
}

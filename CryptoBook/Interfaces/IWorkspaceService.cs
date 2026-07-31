using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IWorkspaceService: IService
    {
        string WorkspaceDirectory { get; }

        void SetWorkspaceDirectory(string path);

        Task<WorkspaceSearchOutcome> SearchFilesAsync(
            string query,
            int maxResults = 200,
            CancellationToken cancellationToken = default);
    }
}

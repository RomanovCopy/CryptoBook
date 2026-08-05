using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IWorkspaceContentSearchService: IService
    {
        Task<WorkspaceContentSearchOutcome> SearchAsync(
            string query,
            IProgress<WorkspaceContentSearchProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}

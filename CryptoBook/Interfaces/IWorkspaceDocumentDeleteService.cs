using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IWorkspaceDocumentDeleteService: IService
    {
        Task<WorkspaceDocumentDeleteResult> DeleteAsync(
            WorkspaceContentSearchResult document,
            CancellationToken cancellationToken = default);
    }
}

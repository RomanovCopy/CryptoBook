using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Единственная точка входа для безопасного перехода к другому файлу.
    /// </summary>
    public interface IDocumentSwitchCoordinator: IService
    {
        Task<WorkspaceFileOpenResult> SwitchAsync(
            string targetPath,
            CancellationToken cancellationToken = default);
    }
}

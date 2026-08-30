using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IWorkspaceFileOpenService
    {
        Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            MediaCatalogSelection? mediaCatalog,
            CancellationToken cancellationToken = default) =>
            OpenAsync(filePath, cancellationToken);

        /// <summary>
        /// Shows the operating-system application picker. Protected files are
        /// exposed only through a read-only decrypted temporary copy.
        /// </summary>
        Task<WorkspaceFileOpenResult> OpenWithAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(WorkspaceFileOpenResult.Fail(
                "Open With is not supported."));

        /// <summary>
        /// Opens a file supplied by an operating-system shell activation.
        /// Protected files must request their key even when another key is
        /// already cached in the current application session.
        /// </summary>
        Task<WorkspaceFileOpenResult> OpenFromShellAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            OpenAsync(filePath, cancellationToken);
    }
}

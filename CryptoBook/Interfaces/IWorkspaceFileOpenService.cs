using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IWorkspaceFileOpenService
    {
        Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default);

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

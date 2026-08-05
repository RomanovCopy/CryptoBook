namespace CryptoBook.Interfaces
{
    public interface IWorkspaceInternalFileOpenService
    {
        Task OpenDocumentAsync(
            string sourcePath,
            string contentPath,
            IFileTemplate contentTemplate,
            bool sourceIsEncrypted,
            CancellationToken cancellationToken = default);
    }
}

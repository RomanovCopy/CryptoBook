namespace CryptoBook.Interfaces
{
    public interface IWorkspaceInternalFileOpenService
    {
        Task OpenDocumentAsync(
            string encryptedPath,
            string decryptedPath,
            IFileTemplate contentTemplate,
            CancellationToken cancellationToken = default);
    }
}

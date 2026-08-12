namespace CryptoBook.Interfaces
{
    public interface IDocumentBackupRecoveryService: IService
    {
        string? GetBackupPath();

        Task<bool> RestoreAsync(
            CancellationToken cancellationToken = default);

        Task SynchronizeAfterEncryptedSaveAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        Task SynchronizeAfterRenameAsync(
            string oldFilePath,
            string newFilePath,
            CancellationToken cancellationToken = default);
    }
}

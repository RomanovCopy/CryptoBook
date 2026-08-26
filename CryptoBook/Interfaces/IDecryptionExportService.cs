using CryptoBook.DTO;
using CryptoBook.Security;

namespace CryptoBook.Interfaces
{
    public interface IDecryptionExportService: IService
    {
        Task<PreparedDecryption> PrepareAsync(
            string sourcePath,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        IReadOnlyList<DecryptionOutputFormat> GetAvailableFormats(
            string originalExtension);

        DecryptionOutputFormat GetDefaultFormat(string originalExtension);

        string GetOutputExtension(
            string originalExtension,
            DecryptionOutputFormat outputFormat);

        Task<string> PublishAsync(
            PreparedDecryption prepared,
            DecryptionOptions options,
            string destinationPath,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);
    }
}

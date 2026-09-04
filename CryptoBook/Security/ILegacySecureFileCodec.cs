using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Security
{
    public interface ILegacySecureFileCodec
    {
        Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        Task<DecryptedFileContent> DecryptFileContentWithLimitAsync(
            string inputFile,
            long maximumContentLength,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        Task<string> ReadOriginalExtensionAsync(
            string inputFile,
            CancellationToken cancellationToken = default);

        Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);
    }
}

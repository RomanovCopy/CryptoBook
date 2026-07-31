using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Security
{
    public interface ISecureFileV2Codec
    {
        Task<bool> HasHeaderAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        Task EncryptFileAsync(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);
    }
}

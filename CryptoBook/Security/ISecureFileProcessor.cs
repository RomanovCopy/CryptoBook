using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    public interface ISecureFileProcessor
    {
        public Task EncryptFileAsync(string inputFile, string outputFile, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);

        Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        public Task DecryptFileAsyncToFile(string inputFile, string outputFile, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);

        Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens protected media without creating a plaintext file. V2 containers
        /// are decrypted block-by-block; legacy containers are held in memory and
        /// rejected when their plaintext exceeds <paramref name="legacyMemoryLimitBytes"/>.
        /// </summary>
        Task<DecryptedFileContent> OpenDecryptedMediaStreamAsync(
            string inputFile,
            long legacyMemoryLimitBytes,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "The secure file processor does not support protected media streams.");

        /// <summary>
        /// Authenticates enough of the protected file to return its embedded
        /// original extension without materializing plaintext on disk.
        /// </summary>
        Task<string> ReadOriginalExtensionAsync(
            string inputFile,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "The secure file processor does not support metadata inspection.");

        public Task<Stream> DecryptFileAsyncToStream(string inputFile, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
    }
}

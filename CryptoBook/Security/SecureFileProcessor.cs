using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Security
{
    public sealed class SecureFileProcessor: ISecureFileProcessor
    {
        private readonly ISecureFileV2Codec _v2Codec;
        private readonly ILegacySecureFileCodec _legacyCodec;

        public SecureFileProcessor(
            ISecureFileV2Codec v2Codec,
            ILegacySecureFileCodec legacyCodec)
        {
            _v2Codec = v2Codec ?? throw new ArgumentNullException(nameof(v2Codec));
            _legacyCodec = legacyCodec ?? throw new ArgumentNullException(nameof(legacyCodec));
        }

        public Task EncryptFileAsync(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            return _v2Codec.EncryptFileAsync(
                inputFile,
                outputFile,
                progress,
                cancellationToken);
        }

        public Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            return _v2Codec.EncryptStreamAsync(
                input,
                originalExtension,
                outputFile,
                progress,
                cancellationToken);
        }

        public async Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            if(await _v2Codec.HasHeaderAsync(inputFile, cancellationToken))
            {
                await _v2Codec.DecryptFileAsyncToFile(
                    inputFile,
                    outputFile,
                    progress,
                    cancellationToken);
                return;
            }

            await _legacyCodec.DecryptFileAsyncToFile(
                inputFile,
                outputFile,
                progress,
                cancellationToken);
        }

        public async Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            DecryptedFileContent decrypted = await DecryptFileContentAsync(
                inputFile,
                progress,
                cancellationToken);
            return decrypted.Content;
        }

        public async Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            if(await _v2Codec.HasHeaderAsync(inputFile, cancellationToken))
            {
                return await _v2Codec.DecryptFileContentAsync(
                    inputFile,
                    progress,
                    cancellationToken);
            }

            return await _legacyCodec.DecryptFileContentAsync(
                inputFile,
                progress,
                cancellationToken);
        }
    }
}

using CryptoBook.Interfaces;
using CryptoBook.Security;

using CryptoBook.Infrastructure;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class MediaSourcePreparationService: IMediaSourcePreparationService
    {
        private readonly ISecureFileValidator _secureFileValidator;
        private readonly ISecureFileProcessor _secureFileProcessor;
        private readonly IEncryptionKeyRequestService _keyRequestService;
        private readonly SecureMediaPlaybackOptions playbackOptions;

        public MediaSourcePreparationService(
            ISecureFileValidator secureFileValidator,
            ISecureFileProcessor secureFileProcessor,
            IEncryptionKeyRequestService keyRequestService,
            SecureMediaPlaybackOptions? playbackOptions = null)
        {
            _secureFileValidator = secureFileValidator ??
                throw new ArgumentNullException(nameof(secureFileValidator));
            _secureFileProcessor = secureFileProcessor ??
                throw new ArgumentNullException(nameof(secureFileProcessor));
            _keyRequestService = keyRequestService ??
                throw new ArgumentNullException(nameof(keyRequestService));
            this.playbackOptions = playbackOptions ?? new SecureMediaPlaybackOptions();
            this.playbackOptions.Validate();
        }

        public async Task<IPreparedMediaSource> PrepareAsync(
            string sourcePath,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            string fullPath = Path.GetFullPath(sourcePath);

            if(!await _secureFileValidator.HasCryptoBookHeaderAsync(
                fullPath,
                cancellationToken))
            {
                return new PreparedMediaSource(
                    fullPath,
                    Path.GetExtension(fullPath));
            }

            if(!_keyRequestService.EnsureKeyAvailable())
                throw new OperationCanceledException(cancellationToken);

            DecryptedFileContent? decrypted = null;
            try
            {
                decrypted = await _secureFileProcessor.OpenDecryptedMediaStreamAsync(
                    fullPath,
                    playbackOptions.LegacyMemoryLimitBytes,
                    cancellationToken: cancellationToken);
                var prepared = new PreparedMediaSource(
                    fullPath,
                    decrypted.OriginalExtension,
                    decrypted.Content);
                decrypted = null;
                return prepared;
            }
            catch
            {
                if(decrypted is not null)
                    await decrypted.DisposeAsync();
                throw;
            }
        }

        private sealed class PreparedMediaSource: IPreparedMediaSource
        {
            private Stream? playbackStream;

            public PreparedMediaSource(string path, string originalExtension)
                : this(path, originalExtension, null)
            {
            }

            public PreparedMediaSource(
                string originalPath,
                string originalExtension,
                Stream? playbackStream)
            {
                OriginalPath = originalPath;
                PlaybackPath = originalPath;
                OriginalExtension = originalExtension;
                this.playbackStream = playbackStream;
            }

            public string OriginalPath { get; }
            public string PlaybackPath { get; }
            public string OriginalExtension { get; }
            public Stream? PlaybackStream => playbackStream;
            public bool IsEncrypted => playbackStream is not null;
            public bool IsTemporary => false;

            public void Dispose()
            {
                playbackStream?.Dispose();
                playbackStream = null;
            }
        }
    }
}

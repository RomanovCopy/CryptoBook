using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class MediaSourcePreparationService: IMediaSourcePreparationService
    {
        private readonly ISecureFileValidator _secureFileValidator;
        private readonly ISecureFileProcessor _secureFileProcessor;

        public MediaSourcePreparationService(
            ISecureFileValidator secureFileValidator,
            ISecureFileProcessor secureFileProcessor)
        {
            _secureFileValidator = secureFileValidator ??
                throw new ArgumentNullException(nameof(secureFileValidator));
            _secureFileProcessor = secureFileProcessor ??
                throw new ArgumentNullException(nameof(secureFileProcessor));
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
                return new PreparedMediaSource(fullPath);
            }

            string temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook",
                "Media",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                string outputBasePath = Path.Combine(temporaryDirectory, "media");
                await _secureFileProcessor.DecryptFileAsyncToFile(
                    fullPath,
                    outputBasePath,
                    cancellationToken: cancellationToken);

                string[] files = Directory.GetFiles(temporaryDirectory);
                if(files.Length != 1)
                    throw new IOException("Не удалось определить расшифрованный медиафайл.");

                return new PreparedMediaSource(
                    fullPath,
                    files[0],
                    temporaryDirectory);
            }
            catch
            {
                TryDeleteDirectory(temporaryDirectory);
                throw;
            }
        }

        private sealed class PreparedMediaSource: IPreparedMediaSource
        {
            private readonly string? _temporaryDirectory;

            public PreparedMediaSource(string path)
                : this(path, path, null)
            {
            }

            public PreparedMediaSource(
                string originalPath,
                string playbackPath,
                string? temporaryDirectory)
            {
                OriginalPath = originalPath;
                PlaybackPath = playbackPath;
                _temporaryDirectory = temporaryDirectory;
            }

            public string OriginalPath { get; }
            public string PlaybackPath { get; }
            public bool IsTemporary => _temporaryDirectory is not null;

            public void Dispose()
            {
                if(_temporaryDirectory is not null)
                    TryDeleteDirectory(_temporaryDirectory);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if(Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch(IOException)
            {
                // Проигрыватель может освободить файл чуть позже.
            }
            catch(UnauthorizedAccessException)
            {
            }
        }
    }
}

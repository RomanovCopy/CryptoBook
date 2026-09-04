using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class MediaSourcePreparationServiceTests: IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));

        public MediaSourcePreparationServiceTests()
        {
            Directory.CreateDirectory(_directory);
        }

        [Fact]
        public async Task UnencryptedMedia_UsesOriginalFileWithoutTemporaryCopy()
        {
            string source = CreateFile("video.mp4", [1, 2, 3]);
            var service = new MediaSourcePreparationService(
                new StubValidator(encrypted: false),
                new StubProcessor(),
                new StubKeyRequest(keyAvailable: false));

            using IPreparedMediaSource prepared = await service.PrepareAsync(source);

            Assert.Equal(Path.GetFullPath(source), prepared.PlaybackPath);
            Assert.False(prepared.IsTemporary);
        }

        [Fact]
        public async Task EncryptedMedia_UsesProtectedStreamWithoutPlaintextFile()
        {
            string source = CreateFile("video.cbook", [9, 8, 7]);
            var processor = new StubProcessor();
            var service = new MediaSourcePreparationService(
                new StubValidator(encrypted: true),
                processor,
                new StubKeyRequest(keyAvailable: true));

            IPreparedMediaSource prepared = await service.PrepareAsync(source);
            string playbackPath = prepared.PlaybackPath;

            Assert.False(prepared.IsTemporary);
            Assert.True(prepared.IsEncrypted);
            Assert.Equal(".mp4", prepared.OriginalExtension);
            Assert.Equal(Path.GetFullPath(source), playbackPath);
            Stream protectedStream = Assert.IsAssignableFrom<Stream>(
                prepared.PlaybackStream);
            using var restored = new MemoryStream();
            await protectedStream.CopyToAsync(restored);
            Assert.Equal([4, 5, 6], restored.ToArray());
            Assert.Equal(Path.GetFullPath(source), processor.InputPath);

            prepared.Dispose();

            Assert.True(File.Exists(source));
            Assert.Throws<ObjectDisposedException>(() =>
                protectedStream.ReadByte());
        }

        [Fact]
        public async Task EncryptedMedia_RequestsKeyBeforeDecrypting()
        {
            string source = CreateFile("video.cbook", [9, 8, 7]);
            var keyRequest = new StubKeyRequest(keyAvailable: false);
            var processor = new StubProcessor();
            var service = new MediaSourcePreparationService(
                new StubValidator(encrypted: true),
                processor,
                keyRequest);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => service.PrepareAsync(source));

            Assert.True(keyRequest.WasRequested);
            Assert.Null(processor.InputPath);
        }

        private string CreateFile(string name, byte[] content)
        {
            string path = Path.Combine(_directory, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose()
        {
            if(Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private sealed class StubValidator(bool encrypted): ISecureFileValidator
        {
            public Task<bool> HasCryptoBookHeaderAsync(
                string filePath,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(encrypted);
        }

        private sealed class StubKeyRequest(bool keyAvailable):
            IEncryptionKeyRequestService
        {
            public bool WasRequested { get; private set; }

            public bool EnsureKeyAvailable()
            {
                WasRequested = true;
                return keyAvailable;
            }
        }

        private sealed class StubProcessor: ISecureFileProcessor
        {
            public string? InputPath { get; private set; }

            public Task EncryptFileAsync(
                string inputFile,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task EncryptStreamAsync(
                Stream input,
                string originalExtension,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task DecryptFileAsyncToFile(
                string inputFile,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                InputPath = inputFile;
                return File.WriteAllBytesAsync(
                    outputFile + ".mp4",
                    [4, 5, 6],
                    cancellationToken);
            }

            public Task<Stream> DecryptFileAsyncToStream(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<DecryptedFileContent> DecryptFileContentAsync(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(new DecryptedFileContent(
                    new MemoryStream([4, 5, 6]),
                    ".mp4"));

            public Task<DecryptedFileContent> OpenDecryptedMediaStreamAsync(
                string inputFile,
                long legacyMemoryLimitBytes,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                InputPath = inputFile;
                return Task.FromResult(new DecryptedFileContent(
                    new MemoryStream([4, 5, 6]),
                    ".mp4"));
            }
        }
    }
}

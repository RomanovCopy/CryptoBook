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
                new StubProcessor());

            using IPreparedMediaSource prepared = await service.PrepareAsync(source);

            Assert.Equal(Path.GetFullPath(source), prepared.PlaybackPath);
            Assert.False(prepared.IsTemporary);
        }

        [Fact]
        public async Task EncryptedMedia_IsDecryptedWithOriginalExtensionAndDeletedOnDispose()
        {
            string source = CreateFile("video.cbook", [9, 8, 7]);
            var processor = new StubProcessor();
            var service = new MediaSourcePreparationService(
                new StubValidator(encrypted: true),
                processor);

            IPreparedMediaSource prepared = await service.PrepareAsync(source);
            string playbackPath = prepared.PlaybackPath;
            string temporaryDirectory = Path.GetDirectoryName(playbackPath)!;

            Assert.True(prepared.IsTemporary);
            Assert.Equal(".mp4", Path.GetExtension(playbackPath));
            Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(playbackPath));
            Assert.Equal(Path.GetFullPath(source), processor.InputPath);

            prepared.Dispose();

            Assert.False(Directory.Exists(temporaryDirectory));
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
        }
    }
}

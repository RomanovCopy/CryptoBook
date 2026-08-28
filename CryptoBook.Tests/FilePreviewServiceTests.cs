using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Security.Cryptography;
using System.Text;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class FilePreviewServiceTests
    {
        [Fact]
        public async Task EncryptedFileWithoutKey_RemainsProtected()
        {
            var source = new StubContentSource(
                encrypted: true,
                hasKey: false,
                content: "secret");
            var service = new FilePreviewService(source);

            FilePreviewContent result = await service.LoadAsync(CreateEncryptedFile());

            Assert.Equal(FilePreviewKind.Protected, result.Kind);
            Assert.Equal(0, source.OpenCount);
        }

        [Fact]
        public async Task EncryptedTextWithValidKey_IsPreviewedFromMemory()
        {
            var source = new StubContentSource(
                encrypted: true,
                hasKey: true,
                content: "расшифрованный текст");
            var service = new FilePreviewService(source);

            FilePreviewContent result = await service.LoadAsync(CreateEncryptedFile());

            Assert.Equal(FilePreviewKind.Text, result.Kind);
            Assert.Contains("расшифрованный", result.Text);
            Assert.Equal(1, source.OpenCount);
        }

        [Fact]
        public async Task EncryptedFileWithInvalidKey_ReturnsPreviewError()
        {
            var source = new StubContentSource(
                encrypted: true,
                hasKey: true,
                error: new CryptographicException("authentication failed"));
            var service = new FilePreviewService(source);

            FilePreviewContent result = await service.LoadAsync(CreateEncryptedFile());

            Assert.Equal(FilePreviewKind.Error, result.Kind);
            Assert.Equal(
                LocalizationManager.Format(
                    "Preview.DecryptFailed",
                    Environment.NewLine,
                    "authentication failed"),
                result.Message);
        }

        [Fact]
        public async Task RemoteTextFile_IsPreviewedThroughContentSource()
        {
            var source = new StubContentSource(
                encrypted: false,
                hasKey: false,
                content: "текст с Android");
            var service = new FilePreviewService(source);
            var file = new FileItem
            {
                Name = "notes",
                FullPath = "mtp://opaque-object-id",
                DisplayPath = @"Shared storage\Download\notes.txt",
                Extension = ".txt",
                Size = 32,
                LastWriteTimeUtc = DateTime.UtcNow
            };

            FilePreviewContent result = await service.LoadAsync(file);

            Assert.Equal(FilePreviewKind.Text, result.Kind);
            Assert.Contains("Android", result.Text);
            Assert.Same(file, source.LastFile);
            Assert.Equal(1, source.OpenCount);
        }

        [Fact]
        public async Task RemoteReadFailure_ReturnsDeviceSpecificMessage()
        {
            var source = new StubContentSource(
                encrypted: false,
                hasKey: false,
                error: new IOException("device disconnected"));
            var service = new FilePreviewService(source);
            var file = new FileItem
            {
                Name = "photo",
                FullPath = "mtp://opaque-object-id",
                Extension = ".jpg",
                Size = 1024,
                LastWriteTimeUtc = DateTime.UtcNow
            };

            FilePreviewContent result = await service.LoadAsync(file);

            Assert.Equal(FilePreviewKind.Error, result.Kind);
            Assert.Equal(
                LocalizationManager.Format(
                    "Preview.RemoteDisplayFailed",
                    Environment.NewLine,
                    "device disconnected"),
                result.Message);
        }

        private static FileItem CreateEncryptedFile() => new()
        {
            Name = "secret.cbook",
            FullPath = @"C:\secret.cbook",
            Extension = ".cbook",
            Size = 1024,
            LastWriteTimeUtc = DateTime.UtcNow
        };

        private sealed class StubContentSource: IFilePreviewContentSource
        {
            private readonly byte[] _content;
            private readonly Exception? _error;
            private readonly bool _encrypted;

            public StubContentSource(
                bool encrypted,
                bool hasKey,
                string content = "",
                Exception? error = null)
            {
                _encrypted = encrypted;
                HasDecryptionKey = hasKey;
                _content = Encoding.UTF8.GetBytes(content);
                _error = error;
            }

            public bool HasDecryptionKey { get; }
            public int OpenCount { get; private set; }
            public IFileItem? LastFile { get; private set; }

            public Task<bool> IsEncryptedAsync(
                IFileItem file,
                CancellationToken cancellationToken = default)
            {
                LastFile = file;
                return Task.FromResult(_encrypted);
            }

            public Task<Stream> OpenReadAsync(
                IFileItem file,
                CancellationToken cancellationToken = default)
            {
                LastFile = file;
                OpenCount++;
                if(_error is not null)
                    throw _error;
                return Task.FromResult<Stream>(
                    new MemoryStream(_content, writable: false));
            }
        }
    }
}

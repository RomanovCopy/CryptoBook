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

            public Task<bool> IsEncryptedAsync(
                string path,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(_encrypted);

            public Task<Stream> OpenReadAsync(
                string path,
                CancellationToken cancellationToken = default)
            {
                OpenCount++;
                if(_error is not null)
                    throw _error;
                return Task.FromResult<Stream>(
                    new MemoryStream(_content, writable: false));
            }
        }
    }
}

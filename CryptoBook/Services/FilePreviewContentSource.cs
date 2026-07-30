using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class FilePreviewContentSource: IFilePreviewContentSource
    {
        private readonly IFileManagerService _fileManagerService;
        private readonly ISecureFileValidator _secureFileValidator;
        private readonly IKeyProvider _keyProvider;

        public FilePreviewContentSource(
            IFileManagerService fileManagerService,
            ISecureFileValidator secureFileValidator,
            IKeyProvider keyProvider)
        {
            _fileManagerService = fileManagerService
                ?? throw new ArgumentNullException(nameof(fileManagerService));
            _secureFileValidator = secureFileValidator
                ?? throw new ArgumentNullException(nameof(secureFileValidator));
            _keyProvider = keyProvider
                ?? throw new ArgumentNullException(nameof(keyProvider));
        }

        public bool HasDecryptionKey => _keyProvider.HasKey;

        public Task<bool> IsEncryptedAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return _secureFileValidator.HasCryptoBookHeaderAsync(
                path,
                cancellationToken);
        }

        public Task<Stream> OpenReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            // FileManager выполняет аутентифицированную расшифровку в память,
            // когда распознаёт контейнер CryptoBook и ключ уже установлен.
            return _fileManagerService.OpenReadAsync(
                path,
                cancellationToken: cancellationToken);
        }
    }
}

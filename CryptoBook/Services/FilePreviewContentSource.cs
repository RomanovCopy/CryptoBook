using CryptoBook.DTO;
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
        private readonly IStorageFacade _storage;

        public FilePreviewContentSource(
            IFileManagerService fileManagerService,
            ISecureFileValidator secureFileValidator,
            IKeyProvider keyProvider,
            IStorageFacade storage)
        {
            _fileManagerService = fileManagerService
                ?? throw new ArgumentNullException(nameof(fileManagerService));
            _secureFileValidator = secureFileValidator
                ?? throw new ArgumentNullException(nameof(secureFileValidator));
            _keyProvider = keyProvider
                ?? throw new ArgumentNullException(nameof(keyProvider));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public bool HasDecryptionKey => _keyProvider.HasKey;

        public Task<bool> IsEncryptedAsync(
            IFileItem file,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            if(!file.Location.IsLocal)
                return Task.FromResult(false);

            return _secureFileValidator.HasCryptoBookHeaderAsync(
                file.Location.OpaqueId,
                cancellationToken);
        }

        public Task<Stream> OpenReadAsync(
            IFileItem file,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            StorageLocation location = file.Location;
            if(!location.IsLocal)
            {
                IStorageProvider provider = _storage.GetProvider(location);
                if(!provider.Capabilities.HasFlag(StorageProviderCapabilities.RawStreams))
                {
                    throw new NotSupportedException(
                        $"Storage provider '{provider.Id}' does not support preview streams.");
                }
                return provider.OpenRawReadAsync(location, cancellationToken);
            }

            // FileManager выполняет аутентифицированную расшифровку в память,
            // когда распознаёт контейнер CryptoBook и ключ уже установлен.
            return _fileManagerService.OpenReadAsync(
                location.OpaqueId,
                cancellationToken: cancellationToken);
        }
    }
}

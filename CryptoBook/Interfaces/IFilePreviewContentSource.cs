using System.IO;

namespace CryptoBook.Interfaces
{
    public interface IFilePreviewContentSource: IService
    {
        bool HasDecryptionKey { get; }
        Task<bool> IsEncryptedAsync(
            IFileItem file,
            CancellationToken cancellationToken = default);
        Task<Stream> OpenReadAsync(
            IFileItem file,
            CancellationToken cancellationToken = default);
    }
}

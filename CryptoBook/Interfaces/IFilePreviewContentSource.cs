using System.IO;

namespace CryptoBook.Interfaces
{
    public interface IFilePreviewContentSource: IService
    {
        bool HasDecryptionKey { get; }
        Task<bool> IsEncryptedAsync(
            string path,
            CancellationToken cancellationToken = default);
        Task<Stream> OpenReadAsync(
            string path,
            CancellationToken cancellationToken = default);
    }
}

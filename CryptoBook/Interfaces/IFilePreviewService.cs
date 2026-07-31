using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IFilePreviewService: IService
    {
        Task<FilePreviewContent> LoadAsync(
            IFileItem file,
            CancellationToken cancellationToken = default);
    }
}

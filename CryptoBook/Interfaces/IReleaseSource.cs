using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IReleaseSource: IService
    {
        Task<ApplicationRelease?> GetLatestAsync(
            CancellationToken cancellationToken = default);
    }
}

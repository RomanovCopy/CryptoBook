using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IApplicationUpdateService: IService
    {
        Task<ApplicationRelease?> CheckAsync(
            CancellationToken cancellationToken = default);
    }
}

using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IUpdateCheckStateStore: IService
    {
        Task<UpdateCheckState> LoadAsync(
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            UpdateCheckState state,
            CancellationToken cancellationToken = default);
    }
}

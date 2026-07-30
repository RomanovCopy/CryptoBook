using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IFavoriteDirectoryStore: IService
    {
        Task<IReadOnlyList<FavoriteDirectory>> LoadAsync(
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            IReadOnlyCollection<FavoriteDirectory> favorites,
            CancellationToken cancellationToken = default);
    }
}

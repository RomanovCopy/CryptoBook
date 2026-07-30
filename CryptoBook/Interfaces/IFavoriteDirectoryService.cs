using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IFavoriteDirectoryService: IService
    {
        event EventHandler? Changed;
        IReadOnlyList<FavoriteDirectory> Items { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<FavoriteDirectory> AddAsync(
            string path,
            CancellationToken cancellationToken = default);
        Task RenameAsync(
            Guid id,
            string displayName,
            CancellationToken cancellationToken = default);
        Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> IsAvailableAsync(
            string path,
            CancellationToken cancellationToken = default);
        string GetDisplayPath(string path);
    }
}

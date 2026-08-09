using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IRecentDocumentStore: IService
    {
        Task<IReadOnlyList<RecentDocument>> LoadAsync(
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            IReadOnlyCollection<RecentDocument> documents,
            CancellationToken cancellationToken = default);
    }
}

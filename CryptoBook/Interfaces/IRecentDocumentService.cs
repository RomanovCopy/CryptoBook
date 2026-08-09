using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IRecentDocumentService: IService
    {
        event EventHandler? Changed;

        IReadOnlyList<RecentDocument> Items { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task RecordOpenedAsync(
            string path,
            CancellationToken cancellationToken = default);
        Task RecordSavedAsync(
            string path,
            CancellationToken cancellationToken = default);
        Task UpdatePathAsync(
            string oldPath,
            string newPath,
            CancellationToken cancellationToken = default);
        Task RemoveAsync(
            string path,
            CancellationToken cancellationToken = default);
    }
}

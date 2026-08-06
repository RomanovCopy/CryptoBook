using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IPinnedDocumentService: IService
    {
        event EventHandler? Changed;

        IReadOnlyList<PinnedDocument> Items { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<PinnedDocument> PinAsync(
            string path,
            CancellationToken cancellationToken = default);
        Task UnpinAsync(
            string path,
            CancellationToken cancellationToken = default);
        Task MarkOpenedAsync(
            string path,
            CancellationToken cancellationToken = default);
        Task UpdatePathAsync(
            string oldPath,
            string newPath,
            CancellationToken cancellationToken = default);
        Task MoveAsync(
            string path,
            int offset,
            CancellationToken cancellationToken = default);
        bool IsPinned(string path);
    }
}

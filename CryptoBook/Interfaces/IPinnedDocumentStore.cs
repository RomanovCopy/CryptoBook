using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IPinnedDocumentStore: IService
    {
        Task<IReadOnlyList<PinnedDocument>> LoadAsync(
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            IReadOnlyCollection<PinnedDocument> documents,
            CancellationToken cancellationToken = default);
    }
}

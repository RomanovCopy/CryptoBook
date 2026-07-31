namespace CryptoBook.Interfaces
{
    public interface IDocumentRecoveryService: IService, IDisposable
    {
        bool HasSnapshot { get; }

        void Start();
        Task<bool> RestoreSnapshotAsync(
            CancellationToken cancellationToken = default);
        Task DeleteSnapshotAsync();
    }
}

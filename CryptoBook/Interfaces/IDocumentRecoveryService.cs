namespace CryptoBook.Interfaces
{
    public interface IDocumentRecoveryService: IService, IDisposable
    {
        bool HasSnapshot { get; }
        DateTimeOffset? DeferredSnapshotTime => null;
        Task<bool> RestoreDeferredSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        void Start();
        Task StopAsync();
        Task<bool> RestoreSnapshotAsync(
            CancellationToken cancellationToken = default);
        Task DeleteSnapshotAsync();
        Task DeferSnapshotAsync() => Task.CompletedTask;
    }
}

using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IStorageFacade: IService
{
    StorageLocation Resolve(string value);
    string Format(StorageLocation location);
    IStorageProvider GetProvider(StorageLocation location);
    StorageProviderCapabilities GetCapabilities(StorageLocation location);

    Task<IReadOnlyList<StorageItemMetadata>> GetRootsAsync(
        CancellationToken cancellationToken = default);
    Task<StorageItemMetadata?> GetMetadataAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageItemMetadata>> GetChildrenAsync(
        StorageLocation location,
        bool includeHidden = false,
        CancellationToken cancellationToken = default);

    StorageLocation? GetParent(StorageLocation location);
    StorageLocation GetChild(StorageLocation container, string name);
    bool AreEquivalent(StorageLocation left, StorageLocation right);
    bool IsDescendant(StorageLocation parent, StorageLocation candidate);

    Task<StorageLocation> CreateUniqueLocationAsync(
        StorageLocation desiredLocation,
        bool isContainer,
        CancellationToken cancellationToken = default);
    Task<long> GetTotalSizeAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageDeletionEntry>> BuildDeletionPlanAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);
}

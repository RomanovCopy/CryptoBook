using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services;

public sealed class StorageFacade: IStorageFacade
{
    private readonly IReadOnlyDictionary<string, IStorageProvider> _providers;

    public StorageFacade(IEnumerable<IStorageProvider> providers)
    {
        _providers = providers
            .GroupBy(provider => provider.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.OrdinalIgnoreCase);
        if(!_providers.ContainsKey(StorageLocation.LocalProviderId))
            throw new InvalidOperationException("The local storage provider is not registered.");
    }

    public StorageLocation Resolve(string value)
    {
        StorageLocation parsed = StorageLocation.Parse(value);
        _ = GetProvider(parsed);
        return parsed;
    }

    public string Format(StorageLocation location) => location.ToString();

    public string FormatDisplayPath(StorageLocation location) =>
        GetProvider(location).FormatDisplayPath(location);

    public StorageLocation ResolveDisplayPath(
        StorageLocation context,
        string displayPath) =>
        GetProvider(context).ResolveDisplayPath(context, displayPath);

    public IStorageProvider GetProvider(StorageLocation location) =>
        _providers.TryGetValue(location.ProviderId, out IStorageProvider? provider)
            ? provider
            : throw new NotSupportedException(
                $"No storage provider is registered for '{location.ProviderId}'.");

    public StorageProviderCapabilities GetCapabilities(StorageLocation location) =>
        GetProvider(location).Capabilities;

    public async Task<IReadOnlyList<StorageItemMetadata>> GetRootsAsync(
        CancellationToken cancellationToken = default)
    {
        var roots = new List<StorageItemMetadata>();
        foreach(IStorageProvider provider in _providers.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            roots.AddRange(await provider.GetRootsAsync(cancellationToken));
        }
        return roots;
    }

    public Task<StorageItemMetadata?> GetMetadataAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default) =>
        GetProvider(location).GetMetadataAsync(location, cancellationToken);

    public Task<IReadOnlyList<StorageItemMetadata>> GetChildrenAsync(
        StorageLocation location,
        bool includeHidden = false,
        CancellationToken cancellationToken = default) =>
        GetProvider(location).GetChildrenAsync(location, includeHidden, cancellationToken);

    public StorageLocation? GetParent(StorageLocation location) =>
        GetProvider(location).GetParent(location);

    public StorageLocation GetChild(StorageLocation container, string name) =>
        GetProvider(container).GetChild(container, name);

    public bool AreEquivalent(StorageLocation left, StorageLocation right) =>
        left.ProviderId.Equals(right.ProviderId, StringComparison.OrdinalIgnoreCase) &&
        GetProvider(left).AreEquivalent(left, right);

    public bool IsDescendant(StorageLocation parent, StorageLocation candidate) =>
        parent.ProviderId.Equals(candidate.ProviderId, StringComparison.OrdinalIgnoreCase) &&
        GetProvider(parent).IsDescendant(parent, candidate);

    public Task<StorageLocation> CreateUniqueLocationAsync(
        StorageLocation desiredLocation,
        bool isContainer,
        CancellationToken cancellationToken = default) =>
        GetProvider(desiredLocation).CreateUniqueLocationAsync(
            desiredLocation,
            isContainer,
            cancellationToken);

    public Task<long> GetTotalSizeAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default) =>
        GetProvider(location).GetTotalSizeAsync(location, cancellationToken);

    public Task<IReadOnlyList<StorageDeletionEntry>> BuildDeletionPlanAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default) =>
        GetProvider(location).BuildDeletionPlanAsync(location, cancellationToken);
}

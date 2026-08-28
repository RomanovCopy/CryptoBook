using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IAndroidDeviceDiscovery: IService
{
    Task<IReadOnlyList<AndroidDeviceInfo>> GetDevicesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Transport boundary for the optional ADB-backed provider. A WPD/MTP provider
/// can use native WPD object ids behind IStorageProvider without changing the
/// explorer model or transfer engine.
/// </summary>
public interface IAndroidStorageBridge: IAndroidDeviceDiscovery
{
    Task<AndroidRemoteEntry?> GetMetadataAsync(
        string serial,
        string objectId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AndroidRemoteEntry>> GetChildrenAsync(
        string serial,
        string containerObjectId,
        CancellationToken cancellationToken = default);
    Task PullAsync(
        string serial,
        string sourceObjectId,
        string localDestination,
        CancellationToken cancellationToken = default);
    Task PushAsync(
        string serial,
        string localSource,
        string destinationObjectId,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(
        string serial,
        string objectId,
        CancellationToken cancellationToken = default);
    Task CopyAsync(
        string serial,
        string sourceObjectId,
        string destinationObjectId,
        CancellationToken cancellationToken = default);
    Task MoveAsync(
        string serial,
        string sourceObjectId,
        string destinationObjectId,
        CancellationToken cancellationToken = default);
    Task CreateContainerAsync(
        string serial,
        string objectId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compatibility marker retained for the ADB-backed Android provider.
/// </summary>
public interface IAndroidDirectTransferProvider: ILocalTransferProvider
{
}

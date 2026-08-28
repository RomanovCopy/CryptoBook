using CryptoBook.DTO;

using System.IO;

namespace CryptoBook.Interfaces;

/// <summary>
/// Testable transport boundary over Windows Portable Devices / MTP.
/// Device ids and relative object paths must remain inside WPD locators.
/// </summary>
public interface IWpdStorageBridge: IService
{
    Task<IReadOnlyList<WpdDeviceInfo>> GetDevicesAsync(
        CancellationToken cancellationToken = default);
    Task<WpdStorageEntry?> GetMetadataAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WpdStorageEntry>> GetChildrenAsync(
        string deviceId,
        string containerRelativePath,
        CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken = default);
    Task<Stream> OpenWriteAsync(
        string deviceId,
        string relativePath,
        bool overwrite,
        CancellationToken cancellationToken = default);
    Task CopyToLocalAsync(
        string deviceId,
        string sourceRelativePath,
        string localDestination,
        CancellationToken cancellationToken = default);
    Task CopyFromLocalAsync(
        string localSource,
        string deviceId,
        string destinationRelativePath,
        CancellationToken cancellationToken = default);
    Task CopyAsync(
        string deviceId,
        string sourceRelativePath,
        string destinationRelativePath,
        CancellationToken cancellationToken = default);
    Task MoveAsync(
        string deviceId,
        string sourceRelativePath,
        string destinationRelativePath,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken = default);
    Task CreateContainerAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken = default);
    Task RenameAsync(
        string deviceId,
        string relativePath,
        string newName,
        CancellationToken cancellationToken = default);
}

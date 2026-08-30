using CryptoBook.DTO;

using System.IO;

namespace CryptoBook.Interfaces;

/// <summary>
/// Provider boundary for local, Android and future WPD/MTP object stores.
/// Locators are opaque outside the implementation.
/// </summary>
public interface IStorageProvider: IService
{
    string Id { get; }
    StorageProviderCapabilities Capabilities { get; }

    string FormatDisplayPath(StorageLocation location) => location.ToString();
    StorageLocation ResolveDisplayPath(
        StorageLocation context,
        string displayPath);

    Task<IReadOnlyList<StorageItemMetadata>> GetRootsAsync(
        CancellationToken cancellationToken = default);

    Task<StorageItemMetadata?> GetMetadataAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorageItemMetadata>> GetChildrenAsync(
        StorageLocation container,
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

    Task<Stream> OpenRawReadAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenRawWriteAsync(
        StorageLocation location,
        bool overwrite,
        CancellationToken cancellationToken = default);

    Task<FileOperationResult> CopyAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);

    Task<FileOperationResult> MoveAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);

    Task<FileOperationResult> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);

    Task<FileOperationResult> CreateContainerAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);

    Task<FileOperationResult> RenameAsync(
        StorageLocation location,
        string newName,
        CancellationToken cancellationToken = default);
}

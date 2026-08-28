using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

/// <summary>
/// Compatibility adapter between the explorer's existing file-provider surface
/// and the WPD IStorageProvider implementation.
/// </summary>
public sealed class WpdFileProviderAdapter: IFileProviderService
{
    private readonly WpdStorageProvider _provider;
    private readonly ISystemItemCreateService _items;

    public WpdFileProviderAdapter(
        WpdStorageProvider provider,
        ISystemItemCreateService items)
    {
        _provider = provider;
        _items = items;
    }

    public string Scheme => WpdLocatorCodec.ProviderId;

    public async Task<List<ISystemItem>> GetContainerContentAsync(
        string path,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default,
        bool includeHidden = false)
    {
        StorageLocation location = ToLocation(path);
        StorageItemMetadata parentMetadata =
            await _provider.GetMetadataAsync(location, cancellationToken) ??
            throw new DirectoryNotFoundException(location.ToString());
        IDriveItem parent = _items.CreateRoot(parentMetadata with
        {
            Kind = StorageItemKind.Root
        });
        IReadOnlyList<StorageItemMetadata> metadata = await _provider.GetChildrenAsync(
            location,
            includeHidden,
            cancellationToken);
        return metadata.Select(item => item.IsContainer
            ? (ISystemItem)_items.CreateDirectory(item, parent)
            : _items.CreateFile(item, parent)).ToList();
    }

    public Task<Stream> OpenReadAsync(
        string path,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default) =>
        _provider.OpenRawReadAsync(ToLocation(path), cancellationToken);

    public Task<Stream> OpenWriteAsync(
        string path,
        bool overwrite,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default) =>
        _provider.OpenRawWriteAsync(ToLocation(path), overwrite, cancellationToken);

    public Task<FileOperationResult> CopyAsync(
        string sourcePath,
        string destinationPath,
        IProgressReporter? progress,
        CancellationToken cancellationToken = default) =>
        _provider.CopyAsync(
            ToLocation(sourcePath),
            ToLocation(destinationPath),
            progress,
            cancellationToken);

    public Task<FileOperationResult> MoveAsync(
        string sourcePath,
        string destinationPath,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default) =>
        _provider.MoveAsync(
            ToLocation(sourcePath),
            ToLocation(destinationPath),
            progress,
            cancellationToken);

    public Task<FileOperationResult> DeleteAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        _provider.DeleteAsync(ToLocation(path), cancellationToken);

    public Task<FileOperationResult> RenameAsync(
        string path,
        string newName,
        CancellationToken cancellationToken = default) =>
        _provider.RenameAsync(ToLocation(path), newName, cancellationToken);

    public async Task<bool> CanReadAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        await _provider.GetMetadataAsync(ToLocation(path), cancellationToken) is not null;

    public async Task<bool> CanWriteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        StorageItemMetadata? metadata = await _provider.GetMetadataAsync(
            ToLocation(path),
            cancellationToken);
        return metadata is null ||
            metadata.Capabilities.HasFlag(StorageProviderCapabilities.Write);
    }

    public Task<FileOperationResult> CreateDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default) =>
        _provider.CreateContainerAsync(ToLocation(directoryPath), cancellationToken);

    public async Task<bool> IsReadOnlyAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        (await _provider.GetMetadataAsync(ToLocation(path), cancellationToken))?.IsReadOnly ?? false;

    public async Task<bool> IsHiddenAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        (await _provider.GetMetadataAsync(ToLocation(path), cancellationToken))?.IsHidden ?? false;

    public Task<FileOperationResult> SetHiddenAsync(
        string path,
        bool hidden,
        CancellationToken cancellationToken = default) => Task.FromResult(
        FileOperationResult.Fail("MTP hidden attributes cannot be changed here."));

    public Task<FileOperationResult> SetReadOnlyAsync(
        string path,
        bool isReadOnly,
        CancellationToken cancellationToken = default) => Task.FromResult(
        FileOperationResult.Fail("MTP read-only attributes cannot be changed here."));

    private static StorageLocation ToLocation(string opaqueId) =>
        new(WpdLocatorCodec.ProviderId, opaqueId);
}

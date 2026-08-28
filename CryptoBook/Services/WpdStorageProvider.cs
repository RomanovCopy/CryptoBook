using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

/// <summary>
/// Windows Portable Devices / MTP storage provider intended as the primary
/// Android transport for ordinary Windows users.
/// </summary>
public sealed class WpdStorageProvider:
    IStorageProvider,
    ILocalTransferProvider
{
    private readonly IWpdStorageBridge _bridge;

    public WpdStorageProvider(IWpdStorageBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public string Id => WpdLocatorCodec.ProviderId;

    public StorageProviderCapabilities Capabilities =>
        StorageProviderCapabilities.Browse |
        StorageProviderCapabilities.Read |
        StorageProviderCapabilities.Write |
        StorageProviderCapabilities.CreateContainer |
        StorageProviderCapabilities.Rename |
        StorageProviderCapabilities.Delete |
        StorageProviderCapabilities.CopyWithinProvider |
        StorageProviderCapabilities.MoveWithinProvider |
        StorageProviderCapabilities.RawStreams |
        StorageProviderCapabilities.Preview;

    public async Task<IReadOnlyList<StorageItemMetadata>> GetRootsAsync(
        CancellationToken cancellationToken = default) =>
        (await _bridge.GetDevicesAsync(cancellationToken))
        .Select(ToRootMetadata)
        .ToArray();

    public async Task<StorageItemMetadata?> GetMetadataAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        (string deviceId, string relativePath) = WpdLocatorCodec.Decode(location);
        if(relativePath == WpdLocatorCodec.RootPath)
        {
            WpdDeviceInfo? device = (await _bridge.GetDevicesAsync(cancellationToken))
                .FirstOrDefault(candidate => candidate.Id.Equals(
                    deviceId,
                    StringComparison.OrdinalIgnoreCase));
            return device is null ? null : ToRootMetadata(device);
        }

        WpdStorageEntry? entry = await _bridge.GetMetadataAsync(
            deviceId,
            relativePath,
            cancellationToken);
        return entry is null ? null : ToMetadata(deviceId, entry);
    }

    public async Task<IReadOnlyList<StorageItemMetadata>> GetChildrenAsync(
        StorageLocation container,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureAvailableAsync(container, cancellationToken);
        (string deviceId, string relativePath) = WpdLocatorCodec.Decode(container);
        IReadOnlyList<WpdStorageEntry> entries = await _bridge.GetChildrenAsync(
            deviceId,
            relativePath,
            cancellationToken);
        return entries
            .Where(entry => includeHidden || !entry.IsHidden)
            .Select(entry => ToMetadata(deviceId, entry))
            .ToArray();
    }

    public StorageLocation? GetParent(StorageLocation location)
    {
        (string deviceId, string relativePath) = WpdLocatorCodec.Decode(location);
        if(relativePath == WpdLocatorCodec.RootPath)
            return null;

        int separator = relativePath.LastIndexOf('/');
        string parent = separator <= 0
            ? WpdLocatorCodec.RootPath
            : relativePath[..separator];
        if(string.IsNullOrEmpty(parent))
            parent = WpdLocatorCodec.RootPath;
        return WpdLocatorCodec.Encode(deviceId, parent);
    }

    public StorageLocation GetChild(StorageLocation container, string name)
    {
        ValidateName(name);
        (string deviceId, string relativePath) = WpdLocatorCodec.Decode(container);
        string child = relativePath == WpdLocatorCodec.RootPath
            ? "/" + name
            : relativePath.TrimEnd('/') + "/" + name;
        return WpdLocatorCodec.Encode(deviceId, child);
    }

    public bool AreEquivalent(StorageLocation left, StorageLocation right)
    {
        (string leftDevice, string leftPath) = WpdLocatorCodec.Decode(left);
        (string rightDevice, string rightPath) = WpdLocatorCodec.Decode(right);
        return leftDevice.Equals(rightDevice, StringComparison.OrdinalIgnoreCase) &&
            leftPath.Equals(rightPath, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsDescendant(StorageLocation parent, StorageLocation candidate)
    {
        (string parentDevice, string parentPath) = WpdLocatorCodec.Decode(parent);
        (string candidateDevice, string candidatePath) = WpdLocatorCodec.Decode(candidate);
        if(!parentDevice.Equals(candidateDevice, StringComparison.OrdinalIgnoreCase))
            return false;
        string prefix = parentPath == WpdLocatorCodec.RootPath
            ? WpdLocatorCodec.RootPath
            : parentPath.TrimEnd('/') + "/";
        return candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !candidatePath.Equals(parentPath, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<StorageLocation> CreateUniqueLocationAsync(
        StorageLocation desiredLocation,
        bool isContainer,
        CancellationToken cancellationToken = default)
    {
        StorageLocation? parent = GetParent(desiredLocation);
        if(parent is null)
            throw new InvalidOperationException("Cannot create a sibling for an MTP device root.");

        (_, string relativePath) = WpdLocatorCodec.Decode(desiredLocation);
        string name = relativePath.Split('/').Last();
        int dot = isContainer ? -1 : name.LastIndexOf('.');
        string baseName = dot > 0 ? name[..dot] : name;
        string extension = dot > 0 ? name[dot..] : string.Empty;

        for(int index = 1; ; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string suffix = index == 1 ? " - Copy" : $" - Copy ({index})";
            StorageLocation candidate = GetChild(
                parent.Value,
                baseName + suffix + extension);
            if(await GetMetadataAsync(candidate, cancellationToken) is null)
                return candidate;
        }
    }

    public async Task<long> GetTotalSizeAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        StorageItemMetadata? metadata = await GetMetadataAsync(location, cancellationToken);
        if(metadata is null)
            return 0;
        if(!metadata.IsContainer)
            return metadata.Size;

        long total = 0;
        foreach(StorageItemMetadata child in await GetChildrenAsync(
            location,
            includeHidden: true,
            cancellationToken))
        {
            total += await GetTotalSizeAsync(child.Location, cancellationToken);
        }
        return total;
    }

    public async Task<IReadOnlyList<StorageDeletionEntry>> BuildDeletionPlanAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        var result = new List<StorageDeletionEntry>();
        await AddDeletionEntriesAsync(location, result, cancellationToken);
        return result;
    }

    public async Task<Stream> OpenRawReadAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        (string deviceId, string relativePath) = WpdLocatorCodec.Decode(location);
        return await _bridge.OpenReadAsync(deviceId, relativePath, cancellationToken);
    }

    public async Task<Stream> OpenRawWriteAsync(
        StorageLocation location,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        (string deviceId, string relativePath) = WpdLocatorCodec.Decode(location);
        return await _bridge.OpenWriteAsync(
            deviceId,
            relativePath,
            overwrite,
            cancellationToken);
    }

    public async Task<FileOperationResult> CopyAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if(!IsSameDevice(source, destination))
            {
                return FileOperationResult.Fail(
                    "Copy between MTP devices requires a temporary local transfer.");
            }
            (string deviceId, string sourcePath) = WpdLocatorCodec.Decode(source);
            (_, string destinationPath) = WpdLocatorCodec.Decode(destination);
            await _bridge.CopyAsync(
                deviceId,
                sourcePath,
                destinationPath,
                cancellationToken);
            progress?.Report(1, source.ToString());
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return Fail(exception);
        }
    }

    public async Task<FileOperationResult> MoveAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if(!IsSameDevice(source, destination))
            {
                return FileOperationResult.Fail(
                    "Move between MTP devices requires copy, verification and delete.");
            }
            (string deviceId, string sourcePath) = WpdLocatorCodec.Decode(source);
            (_, string destinationPath) = WpdLocatorCodec.Decode(destination);
            await _bridge.MoveAsync(
                deviceId,
                sourcePath,
                destinationPath,
                cancellationToken);
            progress?.Report(1, source.ToString());
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return Fail(exception);
        }
    }

    public async Task<FileOperationResult> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (string deviceId, string relativePath) = WpdLocatorCodec.Decode(location);
            if(relativePath == WpdLocatorCodec.RootPath)
                return FileOperationResult.Fail("The MTP device root cannot be deleted.");
            await _bridge.DeleteAsync(deviceId, relativePath, cancellationToken);
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return Fail(exception);
        }
    }

    public async Task<FileOperationResult> CreateContainerAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (string deviceId, string relativePath) = WpdLocatorCodec.Decode(location);
            await _bridge.CreateContainerAsync(deviceId, relativePath, cancellationToken);
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return Fail(exception);
        }
    }

    public async Task<FileOperationResult> RenameAsync(
        StorageLocation location,
        string newName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateName(newName);
            (string deviceId, string relativePath) = WpdLocatorCodec.Decode(location);
            if(relativePath == WpdLocatorCodec.RootPath)
                return FileOperationResult.Fail("The MTP device root cannot be renamed.");
            await _bridge.RenameAsync(
                deviceId,
                relativePath,
                newName,
                cancellationToken);
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return Fail(exception);
        }
    }

    public async Task<FileOperationResult> PullToLocalAsync(
        StorageLocation source,
        StorageLocation localDestination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if(!localDestination.IsLocal)
                return FileOperationResult.Fail("The pull destination must be local storage.");
            (string deviceId, string sourcePath) = WpdLocatorCodec.Decode(source);
            await _bridge.CopyToLocalAsync(
                deviceId,
                sourcePath,
                localDestination.OpaqueId,
                cancellationToken);
            progress?.Report(1, source.ToString());
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return Fail(exception);
        }
    }

    public async Task<FileOperationResult> PushFromLocalAsync(
        StorageLocation localSource,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if(!localSource.IsLocal)
                return FileOperationResult.Fail("The push source must be local storage.");
            (string deviceId, string destinationPath) = WpdLocatorCodec.Decode(destination);
            await _bridge.CopyFromLocalAsync(
                localSource.OpaqueId,
                deviceId,
                destinationPath,
                cancellationToken);
            progress?.Report(1, localSource.ToString());
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return Fail(exception);
        }
    }

    public bool IsSameDevice(StorageLocation left, StorageLocation right)
    {
        (string leftDevice, _) = WpdLocatorCodec.Decode(left);
        (string rightDevice, _) = WpdLocatorCodec.Decode(right);
        return leftDevice.Equals(rightDevice, StringComparison.OrdinalIgnoreCase);
    }

    private StorageItemMetadata ToRootMetadata(WpdDeviceInfo device) => new(
        WpdLocatorCodec.Encode(device.Id, WpdLocatorCodec.RootPath),
        device.DisplayName,
        StorageItemKind.Root,
        Capabilities: device.IsAvailable
            ? Capabilities
            : StorageProviderCapabilities.None,
        DisplayPath: device.DisplayName + " / Shared storage",
        StatusText: device.StatusText);

    private StorageItemMetadata ToMetadata(string deviceId, WpdStorageEntry entry) => new(
        WpdLocatorCodec.Encode(deviceId, entry.RelativePath),
        entry.Name,
        entry.IsContainer ? StorageItemKind.Container : StorageItemKind.File,
        entry.Size,
        entry.LastWriteTimeUtc,
        entry.IsHidden,
        entry.IsReadOnly,
        Capabilities,
        entry.RelativePath,
        "online");

    private async Task EnsureAvailableAsync(
        StorageLocation location,
        CancellationToken cancellationToken)
    {
        (string deviceId, _) = WpdLocatorCodec.Decode(location);
        WpdDeviceInfo? device = (await _bridge.GetDevicesAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id.Equals(
                deviceId,
                StringComparison.OrdinalIgnoreCase));
        if(device is null)
            throw new DirectoryNotFoundException("The MTP device is disconnected.");
        if(device.StatusText.Equals("unauthorized", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                "Unlock the Android device and allow file access before browsing shared storage.");
        if(!device.IsAvailable)
            throw new IOException($"The MTP device is {device.StatusText}.");
    }

    private async Task AddDeletionEntriesAsync(
        StorageLocation location,
        ICollection<StorageDeletionEntry> result,
        CancellationToken cancellationToken)
    {
        StorageItemMetadata? metadata = await GetMetadataAsync(location, cancellationToken);
        if(metadata?.IsContainer == true)
        {
            foreach(StorageItemMetadata child in await GetChildrenAsync(
                location,
                includeHidden: true,
                cancellationToken))
            {
                await AddDeletionEntriesAsync(child.Location, result, cancellationToken);
            }
        }
        result.Add(new StorageDeletionEntry(location, metadata?.Size ?? 0));
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if(name.Contains('/') || name.Contains('\\') || name is "." or "..")
            throw new ArgumentException("Invalid MTP storage item name.", nameof(name));
    }

    private static FileOperationResult Fail(Exception exception)
    {
        string message = string.IsNullOrWhiteSpace(exception.Message)
            ? $"{exception.GetType().Name} (HRESULT 0x{exception.HResult:X8})"
            : $"{exception.Message} (HRESULT 0x{exception.HResult:X8})";
        return FileOperationResult.Fail(message);
    }
}

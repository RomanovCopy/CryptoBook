using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Security.Cryptography;

namespace CryptoBook.Services;

/// <summary>
/// Selects the transfer strategy without exposing provider-specific paths to
/// the coordinator. Cross-provider moves are copy, verify, then delete.
/// </summary>
public sealed class TransferEngine: ITransferEngine
{
    private readonly IStorageFacade _storage;

    public TransferEngine(IStorageFacade storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task<FileOperationResult> CopyAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        IStorageProvider sourceProvider = _storage.GetProvider(source);
        IStorageProvider destinationProvider = _storage.GetProvider(destination);

        if(source.ProviderId.Equals(destination.ProviderId, StringComparison.OrdinalIgnoreCase))
        {
            if(sourceProvider is ILocalTransferProvider remote &&
               !remote.IsSameDevice(source, destination))
            {
                return await CopyBetweenDevicesAsync(
                    source,
                    destination,
                    remote,
                    progress,
                    cancellationToken);
            }
            return await sourceProvider.CopyAsync(
                source,
                destination,
                progress,
                cancellationToken);
        }

        if(source.IsLocal && destinationProvider is ILocalTransferProvider remoteDestination)
        {
            return await remoteDestination.PushFromLocalAsync(
                source,
                destination,
                progress,
                cancellationToken);
        }

        if(destination.IsLocal && sourceProvider is ILocalTransferProvider remoteSource)
        {
            return await remoteSource.PullToLocalAsync(
                source,
                destination,
                progress,
                cancellationToken);
        }

        return await CopyViaRawStreamsAsync(
            source,
            destination,
            progress,
            cancellationToken);
    }

    public async Task<FileOperationResult> MoveAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        IStorageProvider sourceProvider = _storage.GetProvider(source);
        if(source.ProviderId.Equals(destination.ProviderId, StringComparison.OrdinalIgnoreCase) &&
           (sourceProvider is not ILocalTransferProvider remote ||
            remote.IsSameDevice(source, destination)))
        {
            return await sourceProvider.MoveAsync(
                source,
                destination,
                progress,
                cancellationToken);
        }

        FileOperationResult copyResult = await CopyAsync(
            source,
            destination,
            progress,
            cancellationToken);
        if(!copyResult.Success)
            return copyResult;

        cancellationToken.ThrowIfCancellationRequested();
        if(!await VerifyAsync(source, destination, cancellationToken))
        {
            return FileOperationResult.Fail(
                "The copied item could not be verified; the source was not deleted.");
        }

        return await sourceProvider.DeleteAsync(source, cancellationToken);
    }

    private async Task<FileOperationResult> CopyBetweenDevicesAsync(
        StorageLocation source,
        StorageLocation destination,
        ILocalTransferProvider remote,
        IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook",
            "PortableTransfers",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            StorageItemMetadata? metadata = await _storage.GetMetadataAsync(
                source,
                cancellationToken);
            if(metadata is null)
                return FileOperationResult.Fail("Source not found.");

            StorageLocation local = new(
                StorageLocation.LocalProviderId,
                Path.Combine(temporaryRoot, metadata.Name));
            FileOperationResult pull = await remote.PullToLocalAsync(
                source,
                local,
                progress,
                cancellationToken);
            if(!pull.Success)
                return pull;
            return await remote.PushFromLocalAsync(
                local,
                destination,
                progress,
                cancellationToken);
        }
        finally
        {
            try
            {
                if(Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private async Task<FileOperationResult> CopyViaRawStreamsAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        StorageItemMetadata? metadata = await _storage.GetMetadataAsync(
            source,
            cancellationToken);
        if(metadata is null)
            return FileOperationResult.Fail("Source not found.");

        IStorageProvider sourceProvider = _storage.GetProvider(source);
        IStorageProvider destinationProvider = _storage.GetProvider(destination);
        try
        {
            if(metadata.IsContainer)
            {
                FileOperationResult create = await destinationProvider.CreateContainerAsync(
                    destination,
                    cancellationToken);
                if(!create.Success)
                    return create;
                foreach(StorageItemMetadata child in await sourceProvider.GetChildrenAsync(
                    source,
                    includeHidden: true,
                    cancellationToken))
                {
                    StorageLocation childDestination = destinationProvider.GetChild(
                        destination,
                        child.Name);
                    FileOperationResult childResult = await CopyViaRawStreamsAsync(
                        child.Location,
                        childDestination,
                        progress,
                        cancellationToken);
                    if(!childResult.Success)
                        return childResult;
                }
                return FileOperationResult.Ok();
            }

            await using Stream input = await sourceProvider.OpenRawReadAsync(
                source,
                cancellationToken);
            await using Stream output = await destinationProvider.OpenRawWriteAsync(
                destination,
                overwrite: false,
                cancellationToken);
            byte[] buffer = new byte[81920];
            long copied = 0;
            int read;
            while((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copied += read;
                progress?.Report(
                    metadata.Size == 0 ? 1 : (double)copied / metadata.Size,
                    metadata.DisplayPath ?? metadata.Name);
            }
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return FileOperationResult.Fail(exception.Message);
        }
    }

    private async Task<bool> VerifyAsync(
        StorageLocation source,
        StorageLocation destination,
        CancellationToken cancellationToken)
    {
        StorageItemMetadata? sourceMetadata = await _storage.GetMetadataAsync(
            source,
            cancellationToken);
        StorageItemMetadata? destinationMetadata = await _storage.GetMetadataAsync(
            destination,
            cancellationToken);
        if(sourceMetadata is null || destinationMetadata is null ||
           sourceMetadata.IsContainer != destinationMetadata.IsContainer)
            return false;

        if(!sourceMetadata.IsContainer)
        {
            if(sourceMetadata.Size != destinationMetadata.Size)
                return false;

            IStorageProvider sourceProvider = _storage.GetProvider(source);
            IStorageProvider destinationProvider = _storage.GetProvider(destination);
            await using Stream sourceStream = await sourceProvider.OpenRawReadAsync(
                source,
                cancellationToken);
            await using Stream destinationStream = await destinationProvider.OpenRawReadAsync(
                destination,
                cancellationToken);
            byte[] sourceHash = await SHA256.HashDataAsync(
                sourceStream,
                cancellationToken);
            byte[] destinationHash = await SHA256.HashDataAsync(
                destinationStream,
                cancellationToken);
            return CryptographicOperations.FixedTimeEquals(sourceHash, destinationHash);
        }

        IReadOnlyList<StorageItemMetadata> sourceChildren =
            await _storage.GetChildrenAsync(
                source,
                includeHidden: true,
                cancellationToken);
        IReadOnlyList<StorageItemMetadata> destinationChildren =
            await _storage.GetChildrenAsync(
                destination,
                includeHidden: true,
                cancellationToken);
        if(sourceChildren.Count != destinationChildren.Count ||
           !TryIndexByName(destinationChildren, out var destinationByName))
            return false;

        var sourceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach(StorageItemMetadata sourceChild in sourceChildren)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(!sourceNames.Add(sourceChild.Name) ||
               !destinationByName.TryGetValue(
                   sourceChild.Name,
                   out StorageItemMetadata? destinationChild) ||
               !await VerifyAsync(
                   sourceChild.Location,
                   destinationChild.Location,
                   cancellationToken))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryIndexByName(
        IReadOnlyList<StorageItemMetadata> items,
        out Dictionary<string, StorageItemMetadata> itemsByName)
    {
        itemsByName = new Dictionary<string, StorageItemMetadata>(StringComparer.Ordinal);
        foreach(StorageItemMetadata item in items)
        {
            if(!itemsByName.TryAdd(item.Name, item))
                return false;
        }
        return true;
    }
}

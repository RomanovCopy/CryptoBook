using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

const string RemoteTestPrefix = "CryptoBook-WPD-Test-";

string localRoot = Path.Combine(
    Path.GetTempPath(),
    "CryptoBook",
    "WpdIntegration",
    Guid.NewGuid().ToString("N"));
string expectedLocalRoot = Path.GetFullPath(Path.Combine(
    Path.GetTempPath(),
    "CryptoBook",
    "WpdIntegration"));
Directory.CreateDirectory(localRoot);

var bridge = new WindowsPortableDeviceBridge();
var provider = new WpdStorageProvider(bridge);
StorageLocation? remoteTestFolder = null;
bool remoteFolderCreated = false;
bool remoteFolderDeleted = false;
Exception? failure = null;

try
{
    IReadOnlyList<StorageItemMetadata> roots = await provider.GetRootsAsync();
    Console.WriteLine($"MTP roots: {roots.Count}");
    if(roots.Count != 1)
        throw new InvalidOperationException(
            $"Expected exactly one MTP root, found {roots.Count}.");

    IReadOnlyList<StorageItemMetadata> rootChildren = await provider.GetChildrenAsync(
        roots[0].Location);
    StorageItemMetadata[] storageRoots = rootChildren
        .Where(item => item.IsContainer)
        .ToArray();
    if(storageRoots.Length != 1)
    {
        throw new InvalidOperationException(
            $"Expected exactly one MTP storage container, found {storageRoots.Length}.");
    }

    StorageItemMetadata storageRoot = storageRoots[0];
    Console.WriteLine(
        $"Device: {roots[0].Name}; status: {roots[0].StatusText}; " +
        $"storage: {storageRoot.Name}");

    if(args.Contains("--cleanup", StringComparer.OrdinalIgnoreCase))
    {
        StorageItemMetadata[] leftovers = (await provider.GetChildrenAsync(
            storageRoot.Location,
            includeHidden: true))
            .Where(item => item.IsContainer && item.Name.StartsWith(
                RemoteTestPrefix,
                StringComparison.Ordinal))
            .ToArray();
        Console.WriteLine($"Integration leftovers: {leftovers.Length}");
        foreach(StorageItemMetadata leftover in leftovers)
        {
            EnsureSuccess(
                await provider.DeleteAsync(leftover.Location),
                $"Permanent cleanup of {leftover.Name}");
        }
        Console.WriteLine("REMOTE_WPD_CLEANUP: PASS");
        return 0;
    }

    string testName = RemoteTestPrefix + Guid.NewGuid().ToString("N");
    remoteTestFolder = provider.GetChild(storageRoot.Location, testName);
    EnsureSuccess(
        await provider.CreateContainerAsync(remoteTestFolder.Value),
        "Create test folder");
    remoteFolderCreated = true;

    StorageItemMetadata? created = await provider.GetMetadataAsync(
        remoteTestFolder.Value);
    if(created is null || created.Name != testName || !created.IsContainer)
        throw new IOException("Created MTP test folder could not be verified.");

    var localProvider = new LocalStorageProvider();
    var storage = new StorageFacade([localProvider, provider]);
    var engine = new TransferEngine(storage);

    byte[] payload = [0, 1, 2, 3, 127, 128, 254, 255];
    string uploadPath = Path.Combine(localRoot, "upload.bin");
    await File.WriteAllBytesAsync(uploadPath, payload);
    var localUpload = new StorageLocation(
        StorageLocation.LocalProviderId,
        uploadPath);
    StorageLocation remoteUpload = provider.GetChild(
        remoteTestFolder.Value,
        "upload.bin");
    EnsureSuccess(
        await engine.CopyAsync(localUpload, remoteUpload),
        "Local -> MTP push");

    StorageItemMetadata? uploaded = await provider.GetMetadataAsync(remoteUpload);
    if(uploaded?.Size != payload.LongLength)
    {
        throw new IOException(
            $"Uploaded size mismatch: expected {payload.LongLength}, got {uploaded?.Size}.");
    }

    await using(Stream input = await provider.OpenRawReadAsync(remoteUpload))
    await using(var memory = new MemoryStream())
    {
        await input.CopyToAsync(memory);
        if(!payload.AsSpan().SequenceEqual(memory.ToArray()))
            throw new IOException("MTP raw-read content mismatch.");
    }
    Console.WriteLine("MTP raw read: OK");

    StorageLocation remoteCopy = provider.GetChild(
        remoteTestFolder.Value,
        "copy.bin");
    EnsureSuccess(
        await provider.CopyAsync(remoteUpload, remoteCopy),
        "MTP file copy");
    StorageLocation remoteMoved = provider.GetChild(
        remoteTestFolder.Value,
        "moved.bin");
    EnsureSuccess(
        await provider.MoveAsync(remoteCopy, remoteMoved),
        "MTP file move");
    if(await provider.GetMetadataAsync(remoteCopy) is not null)
        throw new IOException("MTP file move left its source behind.");

    EnsureSuccess(
        await provider.RenameAsync(remoteMoved, "renamed.bin"),
        "MTP rename");
    StorageLocation remoteRenamed = provider.GetChild(
        remoteTestFolder.Value,
        "renamed.bin");
    if(await provider.GetMetadataAsync(remoteRenamed) is null)
        throw new IOException("Renamed MTP file was not found.");

    string pullPath = Path.Combine(localRoot, "pulled.bin");
    var localPull = new StorageLocation(StorageLocation.LocalProviderId, pullPath);
    EnsureSuccess(
        await engine.CopyAsync(remoteUpload, localPull),
        "MTP -> Local pull");
    byte[] pulledPayload = await File.ReadAllBytesAsync(pullPath);
    if(!payload.AsSpan().SequenceEqual(pulledPayload))
        throw new IOException("Pulled local content mismatch.");

    string corruptedMovePath = Path.Combine(localRoot, "corrupted-move.bin");
    var localCorruptedMove = new StorageLocation(
        StorageLocation.LocalProviderId,
        corruptedMovePath);
    var corruptingProgress = new SameLengthCorruptingProgressReporter(
        corruptedMovePath);
    FileOperationResult corruptedMove = await engine.MoveAsync(
        remoteUpload,
        localCorruptedMove,
        corruptingProgress);
    if(corruptedMove.Success ||
       corruptedMove.ErrorMessage?.Contains(
           "could not be verified",
           StringComparison.Ordinal) != true)
    {
        throw new IOException(
            "The same-size corruption was not rejected by move verification.");
    }
    if(!corruptingProgress.Corrupted ||
       new FileInfo(corruptedMovePath).Length != payload.LongLength)
    {
        throw new IOException(
            "The integration test did not produce same-size corruption.");
    }
    if(await provider.GetMetadataAsync(remoteUpload) is null)
    {
        throw new IOException(
            "The source was deleted after failed checksum verification.");
    }
    Console.WriteLine("Same-size corruption rejected; MTP source preserved: OK");

    string treePath = Path.Combine(localRoot, "tree");
    string nestedPath = Path.Combine(treePath, "nested");
    Directory.CreateDirectory(nestedPath);
    await File.WriteAllBytesAsync(
        Path.Combine(nestedPath, "child.bin"),
        [9, 8, 7, 6, 5]);
    var localTree = new StorageLocation(StorageLocation.LocalProviderId, treePath);
    StorageLocation remoteTree = provider.GetChild(
        remoteTestFolder.Value,
        "tree");
    EnsureSuccess(
        await engine.CopyAsync(localTree, remoteTree),
        "Local folder -> MTP push");
    if(await provider.GetTotalSizeAsync(remoteTree) != 5)
        throw new IOException("Remote folder size verification failed.");

    StorageLocation remoteTreeCopy = provider.GetChild(
        remoteTestFolder.Value,
        "tree-copy");
    EnsureSuccess(
        await provider.CopyAsync(remoteTree, remoteTreeCopy),
        "MTP folder copy");
    StorageLocation remoteTreeMoved = provider.GetChild(
        remoteTestFolder.Value,
        "tree-moved");
    EnsureSuccess(
        await provider.MoveAsync(remoteTreeCopy, remoteTreeMoved),
        "MTP folder move");
    if(await provider.GetMetadataAsync(remoteTreeCopy) is not null)
        throw new IOException("MTP folder move left its source behind.");
    if(await provider.GetTotalSizeAsync(remoteTreeMoved) != 5)
        throw new IOException("Moved MTP folder size verification failed.");

    string movePullPath = Path.Combine(localRoot, "moved-from-device.bin");
    var localMovePull = new StorageLocation(
        StorageLocation.LocalProviderId,
        movePullPath);
    EnsureSuccess(
        await engine.MoveAsync(remoteRenamed, localMovePull),
        "MTP -> Local verified move");
    if(await provider.GetMetadataAsync(remoteRenamed) is not null)
        throw new IOException("Verified MTP move did not delete its source.");

    await DeleteRemoteTestFolderAsync();
    Console.WriteLine("REMOTE_WPD_INTEGRATION_TEST: PASS");
}
catch(Exception exception)
{
    failure = exception;
    Console.Error.WriteLine("REMOTE_WPD_INTEGRATION_TEST: FAIL");
    Console.Error.WriteLine(exception);
}
finally
{
    if(remoteFolderCreated && !remoteFolderDeleted)
    {
        try
        {
            await DeleteRemoteTestFolderAsync();
            Console.WriteLine("Fallback remote cleanup: OK");
        }
        catch(Exception cleanupException)
        {
            Console.Error.WriteLine($"Fallback remote cleanup failed: {cleanupException}");
            failure ??= cleanupException;
        }
    }

    string resolvedLocalRoot = Path.GetFullPath(localRoot);
    string requiredPrefix = expectedLocalRoot + Path.DirectorySeparatorChar;
    if(!resolvedLocalRoot.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Refusing local cleanup outside integration root: {resolvedLocalRoot}");
    }
    if(Directory.Exists(resolvedLocalRoot))
        Directory.Delete(resolvedLocalRoot, recursive: true);
}

return failure is null ? 0 : 1;

async Task DeleteRemoteTestFolderAsync()
{
    if(remoteTestFolder is null)
        return;

    StorageItemMetadata? metadata = await provider.GetMetadataAsync(
        remoteTestFolder.Value);
    if(metadata is null)
    {
        remoteFolderDeleted = true;
        return;
    }
    if(!metadata.IsContainer ||
       !metadata.Name.StartsWith(RemoteTestPrefix, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Refusing permanent cleanup because the target is not the integration test folder.");
    }

    EnsureSuccess(
        await provider.DeleteAsync(remoteTestFolder.Value),
        "Permanent cleanup of test folder");
    if(await provider.GetMetadataAsync(remoteTestFolder.Value) is not null)
        throw new IOException("Remote test folder still exists after cleanup.");
    remoteFolderDeleted = true;
}

static void EnsureSuccess(FileOperationResult result, string operation)
{
    if(!result.Success)
        throw new IOException($"{operation} failed: {result.ErrorMessage}");
    Console.WriteLine($"{operation}: OK");
}

sealed class SameLengthCorruptingProgressReporter(string path): IProgressReporter
{
    public bool Corrupted { get; private set; }

    public void Report(double? value, string? currentInfo = null)
    {
        if(Corrupted || value != 1)
            return;

        byte[] content = File.ReadAllBytes(path);
        if(content.Length == 0)
            throw new IOException("Cannot corrupt an empty integration payload.");
        content[0] ^= 0xFF;
        File.WriteAllBytes(path, content);
        Corrupted = true;
    }
}

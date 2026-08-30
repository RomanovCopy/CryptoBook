using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

public sealed class AndroidStorageProvider:
    IStorageProvider,
    IAndroidDirectTransferProvider
{
    public const string SharedStorageRoot = "/storage/emulated/0";

    private readonly IAndroidStorageBridge _bridge;

    public AndroidStorageProvider(IAndroidStorageBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public string Id => AndroidLocatorCodec.ProviderId;

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

    public string FormatDisplayPath(StorageLocation location)
    {
        (_, string objectId) = AndroidLocatorCodec.Decode(location);
        return NormalizeRemote(objectId);
    }

    public StorageLocation ResolveDisplayPath(
        StorageLocation context,
        string displayPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayPath);
        (string serial, _) = AndroidLocatorCodec.Decode(context);
        return AndroidLocatorCodec.Encode(serial, NormalizeRemote(displayPath));
    }

    public async Task<IReadOnlyList<StorageItemMetadata>> GetRootsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AndroidDeviceInfo> devices = await _bridge.GetDevicesAsync(
            cancellationToken);
        return devices.Select(ToRootMetadata).ToArray();
    }

    public async Task<StorageItemMetadata?> GetMetadataAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        (string serial, string objectId) = AndroidLocatorCodec.Decode(location);
        if(IsSharedRoot(objectId))
        {
            AndroidDeviceInfo? device = (await _bridge.GetDevicesAsync(cancellationToken))
                .FirstOrDefault(candidate => candidate.Serial.Equals(
                    serial,
                    StringComparison.Ordinal));
            return device is null ? null : ToRootMetadata(device);
        }

        AndroidRemoteEntry? entry = await _bridge.GetMetadataAsync(
            serial,
            objectId,
            cancellationToken);
        return entry is null ? null : ToMetadata(serial, entry);
    }

    public async Task<IReadOnlyList<StorageItemMetadata>> GetChildrenAsync(
        StorageLocation container,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureOnlineAsync(container, cancellationToken);
        (string serial, string objectId) = AndroidLocatorCodec.Decode(container);
        IReadOnlyList<AndroidRemoteEntry> entries = await _bridge.GetChildrenAsync(
            serial,
            objectId,
            cancellationToken);
        return entries
            .Where(entry => includeHidden || !entry.IsHidden)
            .Select(entry => ToMetadata(serial, entry))
            .ToArray();
    }

    public StorageLocation? GetParent(StorageLocation location)
    {
        (string serial, string objectId) = AndroidLocatorCodec.Decode(location);
        string normalized = NormalizeRemote(objectId);
        if(IsSharedRoot(normalized))
            return null;
        int separator = normalized.LastIndexOf('/');
        string parent = separator <= 0 ? SharedStorageRoot : normalized[..separator];
        if(!IsWithinSharedStorage(parent))
            return null;
        return AndroidLocatorCodec.Encode(serial, parent);
    }

    public StorageLocation GetChild(StorageLocation container, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if(name.Contains('/') || name is "." or "..")
            throw new ArgumentException("Invalid Android storage item name.", nameof(name));
        (string serial, string objectId) = AndroidLocatorCodec.Decode(container);
        return AndroidLocatorCodec.Encode(
            serial,
            NormalizeRemote(objectId).TrimEnd('/') + "/" + name);
    }

    public bool AreEquivalent(StorageLocation left, StorageLocation right)
    {
        (string leftSerial, string leftObject) = AndroidLocatorCodec.Decode(left);
        (string rightSerial, string rightObject) = AndroidLocatorCodec.Decode(right);
        return leftSerial.Equals(rightSerial, StringComparison.Ordinal) &&
            NormalizeRemote(leftObject).Equals(
                NormalizeRemote(rightObject),
                StringComparison.Ordinal);
    }

    public bool IsDescendant(StorageLocation parent, StorageLocation candidate)
    {
        (string parentSerial, string parentObject) = AndroidLocatorCodec.Decode(parent);
        (string candidateSerial, string candidateObject) = AndroidLocatorCodec.Decode(candidate);
        if(!parentSerial.Equals(candidateSerial, StringComparison.Ordinal))
            return false;
        string normalizedParent = NormalizeRemote(parentObject).TrimEnd('/');
        string normalizedCandidate = NormalizeRemote(candidateObject);
        return normalizedCandidate.StartsWith(
            normalizedParent + "/",
            StringComparison.Ordinal);
    }

    public async Task<StorageLocation> CreateUniqueLocationAsync(
        StorageLocation desiredLocation,
        bool isContainer,
        CancellationToken cancellationToken = default)
    {
        StorageLocation? parent = GetParent(desiredLocation);
        if(parent is null)
            throw new InvalidOperationException("Cannot create a sibling for a device root.");
        (_, string objectId) = AndroidLocatorCodec.Decode(desiredLocation);
        string name = objectId.Split('/').Last();
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
            cancellationToken.ThrowIfCancellationRequested();
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
        string temporaryPath = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook",
            "Android",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(temporaryPath)!);
        (string serial, string objectId) = AndroidLocatorCodec.Decode(location);
        await _bridge.PullAsync(serial, objectId, temporaryPath, cancellationToken);
        return new TemporaryReadStream(temporaryPath);
    }

    public Task<Stream> OpenRawWriteAsync(
        StorageLocation location,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string temporaryPath = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook",
            "Android",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(temporaryPath)!);
        Stream stream = new AndroidUploadStream(
            temporaryPath,
            location,
            _bridge,
            cancellationToken);
        return Task.FromResult(stream);
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
                return FileOperationResult.Fail(
                    "Copy between Android devices requires a temporary local transfer.");
            (string serial, string sourceObject) = AndroidLocatorCodec.Decode(source);
            (_, string destinationObject) = AndroidLocatorCodec.Decode(destination);
            await _bridge.CopyAsync(
                serial,
                sourceObject,
                destinationObject,
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
            return FileOperationResult.Fail(exception.Message);
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
                return FileOperationResult.Fail(
                    "Move between Android devices requires copy, verification and delete.");
            (string serial, string sourceObject) = AndroidLocatorCodec.Decode(source);
            (_, string destinationObject) = AndroidLocatorCodec.Decode(destination);
            await _bridge.MoveAsync(
                serial,
                sourceObject,
                destinationObject,
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
            return FileOperationResult.Fail(exception.Message);
        }
    }

    public async Task<FileOperationResult> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (_, string objectId) = AndroidLocatorCodec.Decode(location);
            if(IsSharedRoot(objectId))
                return FileOperationResult.Fail("The shared-storage root cannot be deleted.");
            (string serial, _) = AndroidLocatorCodec.Decode(location);
            await _bridge.DeleteAsync(serial, objectId, cancellationToken);
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

    public async Task<FileOperationResult> CreateContainerAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (string serial, string objectId) = AndroidLocatorCodec.Decode(location);
            await _bridge.CreateContainerAsync(serial, objectId, cancellationToken);
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

    public async Task<FileOperationResult> RenameAsync(
        StorageLocation location,
        string newName,
        CancellationToken cancellationToken = default)
    {
        StorageLocation? parent = GetParent(location);
        return parent is null
            ? FileOperationResult.Fail("The shared-storage root cannot be renamed.")
            : await MoveAsync(
                location,
                GetChild(parent.Value, newName),
                cancellationToken: cancellationToken);
    }

    public async Task<FileOperationResult> PullToLocalAsync(
        StorageLocation source,
        StorageLocation localDestination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (string serial, string objectId) = AndroidLocatorCodec.Decode(source);
            await _bridge.PullAsync(
                serial,
                objectId,
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
            return FileOperationResult.Fail(exception.Message);
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
            (string serial, string objectId) = AndroidLocatorCodec.Decode(destination);
            await _bridge.PushAsync(
                serial,
                localSource.OpaqueId,
                objectId,
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
            return FileOperationResult.Fail(exception.Message);
        }
    }

    public bool IsSameDevice(StorageLocation left, StorageLocation right)
    {
        (string leftSerial, _) = AndroidLocatorCodec.Decode(left);
        (string rightSerial, _) = AndroidLocatorCodec.Decode(right);
        return leftSerial.Equals(rightSerial, StringComparison.Ordinal);
    }

    private StorageItemMetadata ToRootMetadata(AndroidDeviceInfo device)
    {
        bool online = device.State == AndroidDeviceState.Online;
        return new StorageItemMetadata(
            AndroidLocatorCodec.Encode(device.Serial, SharedStorageRoot),
            device.DisplayName,
            StorageItemKind.Root,
            Capabilities: online
                ? Capabilities
                : StorageProviderCapabilities.None,
            DisplayPath: device.DisplayName + " / Shared storage",
            StatusText: device.State.ToString().ToLowerInvariant());
    }

    private StorageItemMetadata ToMetadata(string serial, AndroidRemoteEntry entry) => new(
        AndroidLocatorCodec.Encode(serial, entry.Path),
        entry.Name,
        entry.IsContainer ? StorageItemKind.Container : StorageItemKind.File,
        entry.Size,
        entry.LastWriteTimeUtc,
        entry.IsHidden,
        false,
        Capabilities,
        entry.Path);

    private async Task EnsureOnlineAsync(
        StorageLocation location,
        CancellationToken cancellationToken)
    {
        (string serial, _) = AndroidLocatorCodec.Decode(location);
        AndroidDeviceInfo? device = (await _bridge.GetDevicesAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Serial.Equals(serial, StringComparison.Ordinal));
        if(device is null)
            throw new DirectoryNotFoundException("The Android device is disconnected.");
        if(device.State == AndroidDeviceState.Unauthorized)
            throw new UnauthorizedAccessException(
                "Authorize this computer on the Android device before browsing shared storage.");
        if(device.State != AndroidDeviceState.Online)
            throw new IOException($"The Android device is {device.State.ToString().ToLowerInvariant()}.");
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

    private static bool IsSharedRoot(string objectId) =>
        NormalizeRemote(objectId).Equals(SharedStorageRoot, StringComparison.Ordinal);

    private static bool IsWithinSharedStorage(string objectId)
    {
        string normalized = NormalizeRemote(objectId);
        return normalized.Equals(SharedStorageRoot, StringComparison.Ordinal) ||
            normalized.StartsWith(SharedStorageRoot + "/", StringComparison.Ordinal);
    }

    private static string NormalizeRemote(string objectId)
    {
        string normalized = objectId.Replace('\\', '/').TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }

    private sealed class TemporaryReadStream: Stream
    {
        private readonly string _path;
        private readonly FileStream _stream;

        public TemporaryReadStream(string path)
        {
            _path = path;
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;
        public override long Position { get => _stream.Position; set => _stream.Position = value; }
        public override void Flush() => _stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.ReadAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                _stream.Dispose();
                TryDelete(_path);
            }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync();
            TryDelete(_path);
            GC.SuppressFinalize(this);
        }
    }

    private sealed class AndroidUploadStream: Stream
    {
        private readonly string _path;
        private readonly StorageLocation _destination;
        private readonly IAndroidStorageBridge _bridge;
        private readonly CancellationToken _cancellationToken;
        private readonly FileStream _stream;
        private bool _committed;

        public AndroidUploadStream(
            string path,
            StorageLocation destination,
            IAndroidStorageBridge bridge,
            CancellationToken cancellationToken)
        {
            _path = path;
            _destination = destination;
            _bridge = bridge;
            _cancellationToken = cancellationToken;
            _stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        }

        public override bool CanRead => false;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => true;
        public override long Length => _stream.Length;
        public override long Position { get => _stream.Position; set => _stream.Position = value; }
        public override void Flush() => _stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
        public override void SetLength(long value) => _stream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if(disposing && !_committed)
            {
                _stream.Dispose();
                CommitAsync().GetAwaiter().GetResult();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if(!_committed)
            {
                await _stream.DisposeAsync();
                await CommitAsync();
            }
            GC.SuppressFinalize(this);
        }

        private async Task CommitAsync()
        {
            if(_committed)
                return;
            _committed = true;
            try
            {
                (string serial, string objectId) = AndroidLocatorCodec.Decode(_destination);
                await _bridge.PushAsync(serial, _path, objectId, _cancellationToken);
            }
            finally
            {
                TryDelete(_path);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if(File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}

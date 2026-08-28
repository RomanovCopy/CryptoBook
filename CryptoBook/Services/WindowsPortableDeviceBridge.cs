using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Runtime.InteropServices;

using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace CryptoBook.Services;

/// <summary>
/// Ordinary-user Android transport backed by the Windows Portable Devices MTP
/// stack. No ADB executable or developer mode is required.
/// </summary>
public sealed class WindowsPortableDeviceBridge: IWpdStorageBridge
{
    public async Task<IReadOnlyList<WpdDeviceInfo>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(
            StorageDevice.GetDeviceSelector());
        cancellationToken.ThrowIfCancellationRequested();

        var result = new List<WpdDeviceInfo>(devices.Count);
        foreach(DeviceInformation device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool available = device.IsEnabled;
            string status = available ? "online" : "offline";
            if(available)
            {
                try
                {
                    _ = StorageDevice.FromId(device.Id);
                }
                catch(UnauthorizedAccessException)
                {
                    available = false;
                    status = "unauthorized";
                }
                catch(Exception exception) when(
                    exception is IOException or InvalidOperationException or COMException)
                {
                    available = false;
                    status = "offline";
                }
            }

            result.Add(new WpdDeviceInfo(
                device.Id,
                string.IsNullOrWhiteSpace(device.Name) ? "Portable device" : device.Name,
                available,
                status));
        }
        return result;
    }

    public async Task<WpdStorageEntry?> GetMetadataAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            IStorageItem item = await ResolveItemAsync(deviceId, relativePath);
            cancellationToken.ThrowIfCancellationRequested();
            return await ToEntryAsync(relativePath, item, cancellationToken);
        }
        catch(Exception exception) when(IsNotFound(exception))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<WpdStorageEntry>> GetChildrenAsync(
        string deviceId,
        string containerRelativePath,
        CancellationToken cancellationToken = default)
    {
        StorageFolder folder = await ResolveFolderAsync(deviceId, containerRelativePath);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IStorageItem> items = await folder.GetItemsAsync();
        var result = new List<WpdStorageEntry>(items.Count);
        foreach(IStorageItem item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string childPath = CombineRemote(containerRelativePath, item.Name);
            result.Add(await ToEntryAsync(childPath, item, cancellationToken));
        }
        return result;
    }

    public async Task<Stream> OpenReadAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        StorageFile file = await ResolveFileAsync(deviceId, relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        IRandomAccessStreamWithContentType randomAccess = await RetryWhenBusyAsync(
            async () => await file.OpenReadAsync(),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new WpdReadStream(randomAccess);
    }

    public Task<Stream> OpenWriteAsync(
        string deviceId,
        string relativePath,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string temporaryPath = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook",
            "WpdWrites",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(temporaryPath)!);
        Stream stream = new WpdUploadStream(
            temporaryPath,
            deviceId,
            relativePath,
            overwrite,
            this,
            cancellationToken);
        return Task.FromResult(stream);
    }

    public async Task CopyToLocalAsync(
        string deviceId,
        string sourceRelativePath,
        string localDestination,
        CancellationToken cancellationToken = default)
    {
        IStorageItem source = await ResolveItemAsync(deviceId, sourceRelativePath);
        await CopyItemToLocalAsync(source, localDestination, cancellationToken);
    }

    public async Task CopyFromLocalAsync(
        string localSource,
        string deviceId,
        string destinationRelativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(Directory.Exists(localSource))
        {
            (StorageFolder parent, string name) = await ResolveParentAsync(
                deviceId,
                destinationRelativePath);
            StorageFolder destination = await parent.CreateFolderAsync(
                name,
                CreationCollisionOption.FailIfExists);
            await CopyLocalDirectoryContentsAsync(
                localSource,
                destination,
                cancellationToken);
            return;
        }

        if(!File.Exists(localSource))
            throw new FileNotFoundException("Local transfer source was not found.", localSource);

        await CopyLocalFileAsync(
            localSource,
            deviceId,
            destinationRelativePath,
            overwrite: false,
            cancellationToken);
    }

    public async Task CopyAsync(
        string deviceId,
        string sourceRelativePath,
        string destinationRelativePath,
        CancellationToken cancellationToken = default)
    {
        IStorageItem source = await ResolveItemAsync(deviceId, sourceRelativePath);
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook",
            "WpdCopies",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string localStaging = Path.Combine(temporaryRoot, source.Name);
            await CopyItemToLocalAsync(source, localStaging, cancellationToken);
            await CopyFromLocalAsync(
                localStaging,
                deviceId,
                destinationRelativePath,
                cancellationToken);
        }
        finally
        {
            TryDeleteTemporaryDirectory(temporaryRoot);
        }
    }

    public async Task MoveAsync(
        string deviceId,
        string sourceRelativePath,
        string destinationRelativePath,
        CancellationToken cancellationToken = default)
    {
        IStorageItem source = await ResolveItemAsync(deviceId, sourceRelativePath);
        long sourceSize = await GetSizeAsync(source, cancellationToken);
        await CopyAsync(
            deviceId,
            sourceRelativePath,
            destinationRelativePath,
            cancellationToken);
        IStorageItem destination = await ResolveItemAsync(
            deviceId,
            destinationRelativePath);
        long destinationSize = await GetSizeAsync(destination, cancellationToken);
        if(sourceSize != destinationSize)
            throw new IOException("MTP move verification failed; the source was not deleted.");
        await DeleteAsync(deviceId, sourceRelativePath, cancellationToken);
    }

    public async Task DeleteAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        IStorageItem item = await ResolveItemAsync(deviceId, relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await item.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
        catch(Exception exception) when(
            exception is COMException or IOException or InvalidOperationException)
        {
            // Several Android MTP drivers complete deletion asynchronously but
            // return E_UNEXPECTED. Treat it as success only after the item is
            // observably gone; otherwise retry once with the default WPD mode.
            if(await WaitUntilMissingAsync(deviceId, relativePath, cancellationToken))
                return;
            try
            {
                IStorageItem retryItem = await ResolveItemAsync(deviceId, relativePath);
                await retryItem.DeleteAsync(StorageDeleteOption.Default);
            }
            catch(Exception retryException) when(
                retryException is COMException or IOException or InvalidOperationException)
            {
                if(await WaitUntilMissingAsync(deviceId, relativePath, cancellationToken))
                    return;
                throw;
            }
            if(!await WaitUntilMissingAsync(deviceId, relativePath, cancellationToken))
                throw;
        }
    }

    public async Task CreateContainerAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        (StorageFolder parent, string name) = await ResolveParentAsync(deviceId, relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await parent.CreateFolderAsync(name, CreationCollisionOption.FailIfExists);
    }

    public async Task RenameAsync(
        string deviceId,
        string relativePath,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ValidateName(newName);
        IStorageItem item = await ResolveItemAsync(deviceId, relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        await item.RenameAsync(newName, NameCollisionOption.FailIfExists);
    }

    private static async Task<StorageFolder> ResolveFolderAsync(
        string deviceId,
        string relativePath)
    {
        StorageFolder current = GetDeviceRoot(deviceId);
        foreach(string segment in GetSegments(relativePath))
        {
            IStorageItem? child = await current.TryGetItemAsync(segment);
            current = child as StorageFolder ?? throw new DirectoryNotFoundException(
                $"MTP container '{relativePath}' was not found.");
        }
        return current;
    }

    private static async Task<StorageFile> ResolveFileAsync(
        string deviceId,
        string relativePath)
    {
        IStorageItem item = await ResolveItemAsync(deviceId, relativePath);
        return item as StorageFile ?? throw new FileNotFoundException(
            $"MTP file '{relativePath}' was not found.");
    }

    private static async Task<IStorageItem> ResolveItemAsync(
        string deviceId,
        string relativePath)
    {
        string normalized = WpdLocatorCodec.NormalizePath(relativePath);
        if(normalized == WpdLocatorCodec.RootPath)
            return GetDeviceRoot(deviceId);

        (StorageFolder parent, string name) = await ResolveParentAsync(deviceId, normalized);
        return await parent.TryGetItemAsync(name) ?? throw new FileNotFoundException(
            $"MTP item '{relativePath}' was not found.");
    }

    private static async Task<(StorageFolder Parent, string Name)> ResolveParentAsync(
        string deviceId,
        string relativePath)
    {
        string normalized = WpdLocatorCodec.NormalizePath(relativePath);
        string[] segments = GetSegments(normalized);
        if(segments.Length == 0)
            throw new InvalidOperationException("The MTP device root has no parent.");
        string parentPath = segments.Length == 1
            ? WpdLocatorCodec.RootPath
            : "/" + string.Join('/', segments[..^1]);
        return (await ResolveFolderAsync(deviceId, parentPath), segments[^1]);
    }

    private static StorageFolder GetDeviceRoot(string deviceId) =>
        StorageDevice.FromId(deviceId) ?? throw new DirectoryNotFoundException(
            "The portable device is disconnected.");

    private static async Task<WpdStorageEntry> ToEntryAsync(
        string relativePath,
        IStorageItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool isContainer = item is StorageFolder;
        long size = 0;
        DateTime? modified = item.DateCreated.UtcDateTime;
        try
        {
            BasicProperties properties = isContainer
                ? await ((StorageFolder)item).GetBasicPropertiesAsync()
                : await ((StorageFile)item).GetBasicPropertiesAsync();
            modified = properties.DateModified.UtcDateTime;
            if(!isContainer)
                size = checked((long)properties.Size);
        }
        catch(Exception exception) when(
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Some MTP implementations omit optional basic properties. Browsing
            // remains useful with conservative metadata.
        }

        return new WpdStorageEntry(
            WpdLocatorCodec.NormalizePath(relativePath),
            item.Name,
            isContainer,
            size,
            modified,
            item.Name.StartsWith(".", StringComparison.Ordinal));
    }

    private static async Task CopyItemToLocalAsync(
        IStorageItem source,
        string localDestination,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(source is StorageFile file)
        {
            string? parentPath = Path.GetDirectoryName(localDestination);
            if(!string.IsNullOrWhiteSpace(parentPath))
                Directory.CreateDirectory(parentPath);
            IRandomAccessStreamWithContentType randomAccess = await RetryWhenBusyAsync(
                async () => await file.OpenReadAsync(),
                cancellationToken);
            try
            {
                await using Stream input = randomAccess.AsStreamForRead();
                await using Stream output = new FileStream(
                    localDestination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await input.CopyToAsync(output, cancellationToken);
            }
            finally
            {
                randomAccess.Dispose();
            }
            return;
        }

        Directory.CreateDirectory(localDestination);
        foreach(IStorageItem child in await ((StorageFolder)source).GetItemsAsync())
        {
            await CopyItemToLocalAsync(
                child,
                Path.Combine(localDestination, child.Name),
                cancellationToken);
        }
    }

    private static async Task CopyLocalDirectoryContentsAsync(
        string localSource,
        StorageFolder destination,
        CancellationToken cancellationToken)
    {
        foreach(string directory in Directory.EnumerateDirectories(localSource))
        {
            cancellationToken.ThrowIfCancellationRequested();
            StorageFolder childDestination = await destination.CreateFolderAsync(
                Path.GetFileName(directory),
                CreationCollisionOption.FailIfExists);
            await CopyLocalDirectoryContentsAsync(
                directory,
                childDestination,
                cancellationToken);
        }

        foreach(string filePath in Directory.EnumerateFiles(localSource))
        {
            cancellationToken.ThrowIfCancellationRequested();
            StorageFile localFile = await StorageFile.GetFileFromPathAsync(filePath);
            _ = await RetryWhenBusyAsync(
                async () => await localFile.CopyAsync(
                    destination,
                    Path.GetFileName(filePath),
                    NameCollisionOption.FailIfExists),
                cancellationToken);
        }
    }

    private async Task CopyLocalFileAsync(
        string localSource,
        string deviceId,
        string destinationRelativePath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        (StorageFolder destinationParent, string destinationName) =
            await ResolveParentAsync(deviceId, destinationRelativePath);
        cancellationToken.ThrowIfCancellationRequested();
        StorageFile localFile = await StorageFile.GetFileFromPathAsync(localSource);
        _ = await RetryWhenBusyAsync(
            async () => await localFile.CopyAsync(
                destinationParent,
                destinationName,
                overwrite
                    ? NameCollisionOption.ReplaceExisting
                    : NameCollisionOption.FailIfExists),
            cancellationToken);
    }

    private static async Task<T> RetryWhenBusyAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        const int ErrorBusy = unchecked((int)0x800700AA);
        for(int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation();
            }
            catch(COMException exception) when(
                exception.HResult == ErrorBusy && attempt < 5)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(200 * (attempt + 1)),
                    cancellationToken);
            }
        }
    }

    private static async Task<bool> WaitUntilMissingAsync(
        string deviceId,
        string relativePath,
        CancellationToken cancellationToken)
    {
        for(int attempt = 0; attempt < 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _ = await ResolveItemAsync(deviceId, relativePath);
            }
            catch(Exception exception) when(IsNotFound(exception))
            {
                return true;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(150 * (attempt + 1)), cancellationToken);
        }
        return false;
    }

    private static async Task<long> GetSizeAsync(
        IStorageItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(item is StorageFile file)
            return checked((long)(await file.GetBasicPropertiesAsync()).Size);

        long total = 0;
        foreach(IStorageItem child in await ((StorageFolder)item).GetItemsAsync())
            total += await GetSizeAsync(child, cancellationToken);
        return total;
    }

    private static string CombineRemote(string parent, string name)
    {
        ValidateName(name);
        string normalizedParent = WpdLocatorCodec.NormalizePath(parent);
        return normalizedParent == WpdLocatorCodec.RootPath
            ? "/" + name
            : normalizedParent + "/" + name;
    }

    private static string[] GetSegments(string relativePath) =>
        WpdLocatorCodec.NormalizePath(relativePath).Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if(name.Contains('/') || name.Contains('\\') || name is "." or "..")
            throw new ArgumentException("Invalid MTP storage item name.", nameof(name));
    }

    private static bool IsNotFound(Exception exception) =>
        exception is FileNotFoundException or DirectoryNotFoundException;

    private static void TryDeleteTemporaryDirectory(string path)
    {
        try
        {
            if(Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch(IOException)
        {
        }
        catch(UnauthorizedAccessException)
        {
        }
    }

    private sealed class WpdReadStream: Stream
    {
        private readonly IRandomAccessStreamWithContentType _owner;
        private readonly Stream _inner;
        private bool _disposed;

        public WpdReadStream(IRandomAccessStreamWithContentType owner)
        {
            _owner = owner;
            _inner = owner.AsStreamForRead();
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) =>
            _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.Write(buffer, offset, count);

        public override async ValueTask DisposeAsync()
        {
            if(_disposed)
                return;
            _disposed = true;
            try
            {
                await _inner.DisposeAsync();
            }
            finally
            {
                _owner.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing && !_disposed)
            {
                _disposed = true;
                try
                {
                    _inner.Dispose();
                }
                finally
                {
                    _owner.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }

    private sealed class WpdUploadStream: FileStream
    {
        private readonly string _deviceId;
        private readonly string _relativePath;
        private readonly bool _overwrite;
        private readonly WindowsPortableDeviceBridge _owner;
        private readonly CancellationToken _cancellationToken;
        private bool _uploaded;

        public WpdUploadStream(
            string temporaryPath,
            string deviceId,
            string relativePath,
            bool overwrite,
            WindowsPortableDeviceBridge owner,
            CancellationToken cancellationToken)
            : base(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
        {
            _deviceId = deviceId;
            _relativePath = relativePath;
            _overwrite = overwrite;
            _owner = owner;
            _cancellationToken = cancellationToken;
        }

        public override async ValueTask DisposeAsync()
        {
            if(_uploaded)
                return;
            _uploaded = true;
            string temporaryPath = Name;
            try
            {
                await FlushAsync(_cancellationToken);
                await base.DisposeAsync();
                await _owner.CopyLocalFileAsync(
                    temporaryPath,
                    _deviceId,
                    _relativePath,
                    _overwrite,
                    _cancellationToken);
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if(!disposing || _uploaded)
            {
                base.Dispose(disposing);
                return;
            }

            _uploaded = true;
            string temporaryPath = Name;
            try
            {
                Flush();
                base.Dispose(disposing);
                _owner.CopyLocalFileAsync(
                    temporaryPath,
                    _deviceId,
                    _relativePath,
                    _overwrite,
                    _cancellationToken).GetAwaiter().GetResult();
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private static void TryDeleteTemporaryFile(string path)
        {
            try
            {
                if(File.Exists(path))
                    File.Delete(path);
            }
            catch(IOException)
            {
            }
            catch(UnauthorizedAccessException)
            {
            }
        }
    }
}

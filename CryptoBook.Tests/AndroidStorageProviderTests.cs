using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class AndroidStorageProviderTests
{
    [Fact]
    public async Task Roots_PreserveOnlineOfflineAndUnauthorizedStates_WithOpaqueLocators()
    {
        var bridge = new BridgeStub
        {
            Devices =
            [
                new("online-serial", "Pixel", AndroidDeviceState.Online),
                new("offline-serial", "Tablet", AndroidDeviceState.Offline),
                new("unauthorized-serial", "Phone", AndroidDeviceState.Unauthorized)
            ]
        };
        var provider = new AndroidStorageProvider(bridge);

        IReadOnlyList<StorageItemMetadata> roots = await provider.GetRootsAsync();

        Assert.Equal(3, roots.Count);
        Assert.Contains(roots, root => root.StatusText == "online" &&
            root.Capabilities.HasFlag(StorageProviderCapabilities.Browse));
        Assert.Contains(roots, root => root.StatusText == "offline" &&
            root.Capabilities == StorageProviderCapabilities.None);
        Assert.Contains(roots, root => root.StatusText == "unauthorized" &&
            root.Capabilities == StorageProviderCapabilities.None);
        Assert.All(roots, root =>
        {
            Assert.StartsWith("android://", root.Location.ToString());
            Assert.DoesNotContain("serial", root.Location.OpaqueId);
            Assert.DoesNotContain("/storage/", root.Location.OpaqueId);
        });
    }

    [Fact]
    public async Task Browse_MapsSharedStorageEntriesToProviderMetadata()
    {
        var bridge = new BridgeStub
        {
            Devices = [new("device", "Pixel", AndroidDeviceState.Online)],
            Children =
            [
                new("/storage/emulated/0/DCIM", "DCIM", true, 0, null),
                new("/storage/emulated/0/note.txt", "note.txt", false, 12, DateTime.UtcNow)
            ]
        };
        var provider = new AndroidStorageProvider(bridge);
        StorageLocation root = AndroidLocatorCodec.Encode(
            "device",
            AndroidStorageProvider.SharedStorageRoot);

        IReadOnlyList<StorageItemMetadata> items = await provider.GetChildrenAsync(root);

        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Name == "DCIM" && item.IsContainer);
        Assert.Contains(items, item => item.Name == "note.txt" &&
            item.Kind == StorageItemKind.File && item.Size == 12);
    }

    [Fact]
    public async Task TransferEngine_LocalToAndroid_UsesPush()
    {
        using var temporary = new TemporaryDirectory();
        string sourcePath = Path.Combine(temporary.Path, "payload.bin");
        await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
        var bridge = new BridgeStub
        {
            Devices = [new("device", "Pixel", AndroidDeviceState.Online)]
        };
        var android = new AndroidStorageProvider(bridge);
        var storage = new StorageFacade([new LocalStorageProvider(), android]);
        var engine = new TransferEngine(storage);
        StorageLocation destination = AndroidLocatorCodec.Encode(
            "device",
            "/storage/emulated/0/payload.bin");

        FileOperationResult result = await engine.CopyAsync(
            new StorageLocation(StorageLocation.LocalProviderId, sourcePath),
            destination);

        Assert.True(result.Success);
        Assert.Equal(sourcePath, bridge.LastPushedLocalPath);
        Assert.Equal("/storage/emulated/0/payload.bin", bridge.LastPushedObjectId);
    }

    [Fact]
    public async Task TransferEngine_AndroidToLocalMove_VerifiesBeforeDelete()
    {
        using var temporary = new TemporaryDirectory();
        string destinationPath = Path.Combine(temporary.Path, "payload.bin");
        var bridge = new BridgeStub
        {
            Devices = [new("device", "Pixel", AndroidDeviceState.Online)],
            Metadata = new AndroidRemoteEntry(
                "/storage/emulated/0/payload.bin",
                "payload.bin",
                false,
                3,
                DateTime.UtcNow),
            PulledContent = [1, 2, 3]
        };
        var android = new AndroidStorageProvider(bridge);
        var storage = new StorageFacade([new LocalStorageProvider(), android]);
        var engine = new TransferEngine(storage);
        StorageLocation source = AndroidLocatorCodec.Encode(
            "device",
            "/storage/emulated/0/payload.bin");

        FileOperationResult result = await engine.MoveAsync(
            source,
            new StorageLocation(StorageLocation.LocalProviderId, destinationPath));

        Assert.True(result.Success);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal("/storage/emulated/0/payload.bin", bridge.DeletedObjectId);
        Assert.True(bridge.PullCompletedBeforeDelete);
    }

    private sealed class BridgeStub: IAndroidStorageBridge
    {
        public IReadOnlyList<AndroidDeviceInfo> Devices { get; init; } = [];
        public IReadOnlyList<AndroidRemoteEntry> Children { get; init; } = [];
        public AndroidRemoteEntry? Metadata { get; init; }
        public byte[] PulledContent { get; init; } = [];
        public string? LastPushedLocalPath { get; private set; }
        public string? LastPushedObjectId { get; private set; }
        public string? DeletedObjectId { get; private set; }
        public bool PullCompletedBeforeDelete { get; private set; }
        private bool _pullCompleted;

        public Task<IReadOnlyList<AndroidDeviceInfo>> GetDevicesAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(Devices);

        public Task<AndroidRemoteEntry?> GetMetadataAsync(
            string serial,
            string objectId,
            CancellationToken cancellationToken = default) => Task.FromResult(
                Metadata is not null && Metadata.Path == objectId
                    ? Metadata
                    : Children.FirstOrDefault(entry => entry.Path == objectId));

        public Task<IReadOnlyList<AndroidRemoteEntry>> GetChildrenAsync(
            string serial,
            string containerObjectId,
            CancellationToken cancellationToken = default) => Task.FromResult(Children);

        public async Task PullAsync(
            string serial,
            string sourceObjectId,
            string localDestination,
            CancellationToken cancellationToken = default)
        {
            await File.WriteAllBytesAsync(localDestination, PulledContent, cancellationToken);
            _pullCompleted = true;
        }

        public Task PushAsync(
            string serial,
            string localSource,
            string destinationObjectId,
            CancellationToken cancellationToken = default)
        {
            LastPushedLocalPath = localSource;
            LastPushedObjectId = destinationObjectId;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string serial,
            string objectId,
            CancellationToken cancellationToken = default)
        {
            DeletedObjectId = objectId;
            PullCompletedBeforeDelete = _pullCompleted;
            return Task.CompletedTask;
        }

        public Task CopyAsync(string serial, string sourceObjectId, string destinationObjectId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task MoveAsync(string serial, string sourceObjectId, string destinationObjectId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task CreateContainerAsync(string serial, string objectId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TemporaryDirectory: IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if(Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class WpdStorageProviderTests
{
    [Fact]
    public async Task Roots_ExposeAvailableMtpDevices_WithOpaqueLocators()
    {
        var bridge = new BridgeStub
        {
            Devices =
            [
                new("wpd-device-id", "Pixel", true, "online"),
                new("locked-device-id", "Locked phone", false, "unauthorized")
            ]
        };
        var provider = new WpdStorageProvider(bridge);

        IReadOnlyList<StorageItemMetadata> roots = await provider.GetRootsAsync();

        Assert.Equal(2, roots.Count);
        StorageItemMetadata online = Assert.Single(roots, root => root.Name == "Pixel");
        Assert.Equal("mtp", online.Location.ProviderId);
        Assert.DoesNotContain("wpd-device-id", online.Location.OpaqueId);
        Assert.True(online.Capabilities.HasFlag(StorageProviderCapabilities.Browse));
        Assert.True(online.Capabilities.HasFlag(StorageProviderCapabilities.Preview));
        Assert.False(online.Capabilities.HasFlag(StorageProviderCapabilities.Encrypt));
        StorageItemMetadata locked = Assert.Single(
            roots,
            root => root.StatusText == "unauthorized");
        Assert.Equal(StorageProviderCapabilities.None, locked.Capabilities);
    }

    [Fact]
    public async Task Browse_MapsEntries_WithoutExposingTransportPaths()
    {
        var bridge = new BridgeStub
        {
            Devices = [new("device", "Phone")]
        };
        bridge.Entries[("device", "/DCIM")] = new WpdStorageEntry(
            "/DCIM",
            "DCIM",
            true,
            0,
            null);
        bridge.Entries[("device", "/note.txt")] = new WpdStorageEntry(
            "/note.txt",
            "note.txt",
            false,
            12,
            DateTime.UtcNow);
        var provider = new WpdStorageProvider(bridge);
        StorageLocation root = WpdLocatorCodec.Encode("device", "/");

        IReadOnlyList<StorageItemMetadata> children = await provider.GetChildrenAsync(root);

        Assert.Equal(2, children.Count);
        Assert.Contains(children, item => item.Name == "DCIM" && item.IsContainer);
        StorageItemMetadata file = Assert.Single(children, item => item.Name == "note.txt");
        Assert.Equal(12, file.Size);
        Assert.DoesNotContain("/note.txt", file.Location.OpaqueId);
        Assert.Equal(root, provider.GetParent(file.Location));
    }

    [Fact]
    public async Task TransferEngine_MtpDeviceToMtpDevice_UsesLocalStaging()
    {
        var bridge = new BridgeStub
        {
            Devices = [new("device-a", "Phone A"), new("device-b", "Phone B")]
        };
        bridge.Entries[("device-a", "/payload.bin")] = new WpdStorageEntry(
            "/payload.bin",
            "payload.bin",
            false,
            3,
            DateTime.UtcNow);
        bridge.Content[("device-a", "/payload.bin")] = [1, 2, 3];
        var mtp = new WpdStorageProvider(bridge);
        var storage = new StorageFacade([new LocalStorageProvider(), mtp]);
        var engine = new TransferEngine(storage);

        FileOperationResult result = await engine.CopyAsync(
            WpdLocatorCodec.Encode("device-a", "/payload.bin"),
            WpdLocatorCodec.Encode("device-b", "/payload.bin"));

        Assert.True(result.Success);
        Assert.True(bridge.CopyToLocalCalled);
        Assert.True(bridge.CopyFromLocalCalled);
        Assert.Equal(
            new byte[] { 1, 2, 3 },
            bridge.Content[("device-b", "/payload.bin")]);
    }

    [Fact]
    public async Task TransferEngine_CrossDeviceMove_VerifiesBeforePermanentDelete()
    {
        var bridge = new BridgeStub
        {
            Devices = [new("device-a", "Phone A"), new("device-b", "Phone B")]
        };
        bridge.Entries[("device-a", "/payload.bin")] = new WpdStorageEntry(
            "/payload.bin",
            "payload.bin",
            false,
            3,
            DateTime.UtcNow);
        bridge.Content[("device-a", "/payload.bin")] = [4, 5, 6];
        var mtp = new WpdStorageProvider(bridge);
        var storage = new StorageFacade([new LocalStorageProvider(), mtp]);
        var engine = new TransferEngine(storage);
        StorageLocation source = WpdLocatorCodec.Encode("device-a", "/payload.bin");

        FileOperationResult result = await engine.MoveAsync(
            source,
            WpdLocatorCodec.Encode("device-b", "/payload.bin"));

        Assert.True(result.Success);
        Assert.True(bridge.DestinationExistedBeforeDelete);
        Assert.Null(await mtp.GetMetadataAsync(source));
    }

    private sealed class BridgeStub: IWpdStorageBridge
    {
        public IReadOnlyList<WpdDeviceInfo> Devices { get; init; } = [];
        public Dictionary<(string Device, string Path), WpdStorageEntry> Entries { get; } = [];
        public Dictionary<(string Device, string Path), byte[]> Content { get; } = [];
        public bool CopyToLocalCalled { get; private set; }
        public bool CopyFromLocalCalled { get; private set; }
        public bool DestinationExistedBeforeDelete { get; private set; }

        public Task<IReadOnlyList<WpdDeviceInfo>> GetDevicesAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(Devices);

        public Task<WpdStorageEntry?> GetMetadataAsync(
            string deviceId,
            string relativePath,
            CancellationToken cancellationToken = default) => Task.FromResult(
                Entries.TryGetValue((deviceId, relativePath), out WpdStorageEntry? entry)
                    ? entry
                    : null);

        public Task<IReadOnlyList<WpdStorageEntry>> GetChildrenAsync(
            string deviceId,
            string containerRelativePath,
            CancellationToken cancellationToken = default)
        {
            string prefix = containerRelativePath == "/"
                ? "/"
                : containerRelativePath.TrimEnd('/') + "/";
            WpdStorageEntry[] children = Entries
                .Where(pair => pair.Key.Device == deviceId &&
                    pair.Key.Path.StartsWith(prefix, StringComparison.Ordinal) &&
                    !pair.Key.Path[prefix.Length..].Contains('/'))
                .Select(pair => pair.Value)
                .ToArray();
            return Task.FromResult<IReadOnlyList<WpdStorageEntry>>(children);
        }

        public Task<Stream> OpenReadAsync(
            string deviceId,
            string relativePath,
            CancellationToken cancellationToken = default) => Task.FromResult<Stream>(
                new MemoryStream(Content[(deviceId, relativePath)], writable: false));

        public Task<Stream> OpenWriteAsync(
            string deviceId,
            string relativePath,
            bool overwrite,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task CopyToLocalAsync(
            string deviceId,
            string sourceRelativePath,
            string localDestination,
            CancellationToken cancellationToken = default)
        {
            CopyToLocalCalled = true;
            await File.WriteAllBytesAsync(
                localDestination,
                Content[(deviceId, sourceRelativePath)],
                cancellationToken);
        }

        public async Task CopyFromLocalAsync(
            string localSource,
            string deviceId,
            string destinationRelativePath,
            CancellationToken cancellationToken = default)
        {
            CopyFromLocalCalled = true;
            byte[] content = await File.ReadAllBytesAsync(localSource, cancellationToken);
            Content[(deviceId, destinationRelativePath)] = content;
            Entries[(deviceId, destinationRelativePath)] = new WpdStorageEntry(
                destinationRelativePath,
                destinationRelativePath.Split('/').Last(),
                false,
                content.LongLength,
                DateTime.UtcNow);
        }

        public Task CopyAsync(
            string deviceId,
            string sourceRelativePath,
            string destinationRelativePath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MoveAsync(
            string deviceId,
            string sourceRelativePath,
            string destinationRelativePath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(
            string deviceId,
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            DestinationExistedBeforeDelete = Entries.ContainsKey(("device-b", "/payload.bin"));
            Entries.Remove((deviceId, relativePath));
            Content.Remove((deviceId, relativePath));
            return Task.CompletedTask;
        }

        public Task CreateContainerAsync(
            string deviceId,
            string relativePath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RenameAsync(
            string deviceId,
            string relativePath,
            string newName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

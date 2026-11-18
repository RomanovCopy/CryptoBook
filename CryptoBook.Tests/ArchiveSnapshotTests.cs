using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.IO.Compression;
using System.Reflection;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ArchiveSnapshotTests
{
    [Fact]
    public async Task PortableAndNestedArchives_AreReadOnly_KeepDisplayAndParent_AndRemoveSnapshots()
    {
        using var directory = new ArchiveTestDirectory();
        var service = directory.CreateService();
        var archives = new ArchiveStorageProvider(service, DispatchProxy.Create<ISystemItemCreateService, ArchiveUpdatingTests.UnusedItems>());
        var portable = DispatchProxy.Create<IMemoryProvider, MemoryProvider>();
        var memory = (MemoryProvider)portable;
        memory.Bytes = Zip("inner.zip", Zip("document.txt", "nested document"u8.ToArray()));
        var storage = new StorageFacade([new LocalStorageProvider(), archives, portable]);
        var source = new StorageLocation("portable", "opaque-file-id");
        Guid owner = Guid.NewGuid();
        string? outerSnapshot = null, innerSnapshot = null;
        try
        {
            var root = await archives.MountSourceAsync(owner, source, storage, null, default);
            outerSnapshot = archives.SourcePath(root);
            Assert.Equal(new StorageLocation("portable", "root"), archives.GetParent(root));
            Assert.Equal("Phone/outer.zip!/", archives.FormatDisplayPath(root));
            Assert.False(archives.CanEdit(root));
            var nestedFile = Assert.Single(await archives.GetChildrenAsync(root));
            var nested = await archives.MountSourceAsync(owner, nestedFile.Location, storage, null, default);
            innerSnapshot = archives.SourcePath(nested);
            Assert.Equal(root, archives.GetParent(nested));
            Assert.Equal("Phone/outer.zip!/inner.zip!/", archives.FormatDisplayPath(nested));
            var document = Assert.Single(await archives.GetChildrenAsync(nested));
            Assert.True(document.IsReadOnly);
            Assert.False((await archives.DeleteAsync(document.Location)).Success);
            await using(var input = await archives.OpenRawReadAsync(document.Location))
            using(var reader = new StreamReader(input)) Assert.Equal("nested document", await reader.ReadToEndAsync());
            // Exercise archive -> portable transfer through a verified local extraction.
            var copied = await new TransferEngine(storage).CopyAsync(document.Location, new("portable", "destination"));
            Assert.True(copied.Success, copied.ErrorMessage);
            Assert.Equal("nested document", System.Text.Encoding.UTF8.GetString(memory.Uploaded!));
        }
        finally { archives.CloseOwner(owner); }
        Assert.NotNull(outerSnapshot);
        Assert.False(File.Exists(outerSnapshot));
        Assert.False(File.Exists(innerSnapshot));
        Assert.Equal(Zip("inner.zip", Zip("document.txt", "nested document"u8.ToArray())).Length, memory.Bytes.Length);
    }

    [Fact]
    public async Task TruncatedPortableSnapshot_IsRejected()
    {
        using var directory = new ArchiveTestDirectory();
        var archives = new ArchiveStorageProvider(directory.CreateService(), DispatchProxy.Create<ISystemItemCreateService, ArchiveUpdatingTests.UnusedItems>());
        var portable = DispatchProxy.Create<IMemoryProvider, MemoryProvider>();
        var memory = (MemoryProvider)portable;
        memory.Bytes = Zip("file.txt", "text"u8.ToArray());
        memory.DeclaredExtraBytes = 1;
        var storage = new StorageFacade([new LocalStorageProvider(), archives, portable]);
        await Assert.ThrowsAsync<InvalidDataException>(() => archives.MountSourceAsync(Guid.NewGuid(),
            new("portable", "opaque"), storage, null, default));
    }

    private static byte[] Zip(string name, byte[] content)
    {
        using var result = new MemoryStream();
        using(var zip = new ZipArchive(result, ZipArchiveMode.Create, true))
        using(var output = zip.CreateEntry(name).Open()) output.Write(content);
        return result.ToArray();
    }

    public interface IMemoryProvider: IStorageProvider, ILocalTransferProvider { }
    public class MemoryProvider: DispatchProxy
    {
        public byte[] Bytes { get; set; } = [];
        public byte[]? Uploaded { get; private set; }
        public int DeclaredExtraBytes { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_Id" => "portable",
            "get_Capabilities" => StorageProviderCapabilities.Browse | StorageProviderCapabilities.Read | StorageProviderCapabilities.Write,
            "GetMetadataAsync" => Task.FromResult<StorageItemMetadata?>(new((StorageLocation)args![0]!, "outer.zip", StorageItemKind.File, Bytes.Length + DeclaredExtraBytes)),
            "FormatDisplayPath" => "Phone/outer.zip",
            "GetParent" => new StorageLocation("portable", "root"),
            "OpenRawReadAsync" => Task.FromResult<Stream>(new MemoryStream(Bytes, false)),
            "PushFromLocalAsync" => UploadAsync((StorageLocation)args![0]!),
            _ => throw new NotSupportedException(method.Name)
        };
        private async Task<FileOperationResult> UploadAsync(StorageLocation source)
        {
            Uploaded = await File.ReadAllBytesAsync(source.OpaqueId);
            return FileOperationResult.Ok();
        }
    }
}

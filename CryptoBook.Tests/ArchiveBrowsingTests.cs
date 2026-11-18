using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;

using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ArchiveBrowsingTests
{
    [Fact]
    public async Task Browse_InfersDirectories_StreamsLiteralUnicodeNames_AndCopiesOnlySelection()
    {
        using var fixture = new Fixture();
        string archive = Path.Combine(fixture.Directory.Path, "Снимок.zip");
        using(var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            Add(zip, "Папка/документ_@1.txt", "Текст 🔐");
            Add(zip, "Папка/-другой.txt", "other");
            Add(zip, "пустая/", "");
            Add(zip, "не выбран.txt", "private");
        }
        byte[] before = await File.ReadAllBytesAsync(archive);
        var root = await fixture.OpenAsync(archive);
        var children = await fixture.Provider.GetChildrenAsync(root);
        Assert.Equal(3, children.Count);
        var folder = children.Single(item => item.Name == "Папка");
        Assert.True(folder.IsContainer);
        var files = await fixture.Provider.GetChildrenAsync(folder.Location);
        var text = files.Single(item => item.Name == "документ_@1.txt");
        Assert.False(text.IsReadOnly);
        Assert.True(text.Capabilities.HasFlag(StorageProviderCapabilities.Write));
        Assert.EndsWith("Снимок.zip!/Папка/документ_@1.txt", text.DisplayPath);
        await using(var input = await fixture.Provider.OpenRawReadAsync(text.Location))
        using(var reader = new StreamReader(input)) Assert.Equal("Текст 🔐", await reader.ReadToEndAsync());
        var display = fixture.Provider.FormatDisplayPath(text.Location);
        Assert.True(fixture.Provider.AreEquivalent(text.Location, fixture.Provider.ResolveDisplayPath(root, display)));
        Assert.True(fixture.Provider.AreEquivalent(folder.Location, fixture.Provider.GetParent(text.Location)!.Value));
        Assert.True(fixture.Provider.IsDescendant(folder.Location, text.Location));
        var output = new StorageLocation("local", Path.Combine(fixture.Directory.Path, "Выбрано"));
        Assert.True((await fixture.Provider.PullToLocalAsync(folder.Location, output)).Success);
        Assert.Equal("Текст 🔐", await File.ReadAllTextAsync(Path.Combine(output.OpaqueId, "документ_@1.txt")));
        Assert.Equal(2, System.IO.Directory.GetFiles(output.OpaqueId).Length);
        Assert.False(File.Exists(Path.Combine(output.OpaqueId, "не выбран.txt")));
        var empty = children.Single(item => item.Name == "пустая");
        string emptyOutput = Path.Combine(fixture.Directory.Path, "empty-output");
        Assert.True((await fixture.Provider.PullToLocalAsync(empty.Location, new("local", emptyOutput))).Success);
        Assert.Empty(System.IO.Directory.GetFileSystemEntries(emptyOutput));
        // Source stays stable until its owner releases the session.
        Assert.Throws<IOException>(() => File.Open(archive, FileMode.Open, FileAccess.Write, FileShare.None));
        fixture.Provider.CloseOwner(fixture.Owner);
        Assert.Equal(before, await File.ReadAllBytesAsync(archive));
        Assert.Empty(System.IO.Directory.GetDirectories(fixture.Directory.Path, ".cryptobook-extract-*"));
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip, "ascii password")]
    [InlineData(ArchiveFormat.SevenZip, "Пароль 🔐")]
    public async Task EncryptedArchive_RequiresPassword_RejectsWrongPassword_AndReadsWithoutPlaintextTemp(ArchiveFormat format, string password)
    {
        using var fixture = new Fixture();
        string input = Path.Combine(fixture.Directory.Path, "file.txt");
        await File.WriteAllTextAsync(input, "encrypted content");
        string archive = Path.Combine(fixture.Directory.Path, format == ArchiveFormat.Zip ? "a.zip" : "a.7z");
        await fixture.Service.CreateAsync([input], archive, format, password);
        await Assert.ThrowsAsync<ArchivePasswordRequiredException>(() => fixture.OpenAsync(archive));
        await Assert.ThrowsAsync<ArchivePasswordRequiredException>(() => fixture.OpenAsync(archive, "wrong"));
        var root = await fixture.OpenAsync(archive, password);
        var file = Assert.Single(await fixture.Provider.GetChildrenAsync(root));
        await using var stream = await fixture.Provider.OpenRawReadAsync(file.Location);
        using var reader = new StreamReader(stream);
        Assert.Equal("encrypted content", await reader.ReadToEndAsync());
        Assert.Equal(2, System.IO.Directory.GetFiles(fixture.Directory.Path).Length);
        Assert.Empty(System.IO.Directory.GetDirectories(fixture.Directory.Path, ".cryptobook-*"));
    }

    [Theory]
    [InlineData("Rar.rar")]
    [InlineData("Rar5.rar")]
    [InlineData("Rar.solid.rar")]
    [InlineData("Rar5.solid.rar")]
    public async Task RarBrowsing_StreamsTheExactSelectedEntry(string name)
    {
        using var fixture = new Fixture();
        string archive = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Archives", name);
        var root = await fixture.OpenAsync(archive);
        var folder = (await fixture.Provider.GetChildrenAsync(root)).Single(item => item.Name == "jpg");
        var file = Assert.Single(await fixture.Provider.GetChildrenAsync(folder.Location));
        await using var stream = await fixture.Provider.OpenRawReadAsync(file.Location);
        Assert.Equal("B251C7501FB0F55DD4A92FEABE0A6F5733BC40A02679498155FAE9B30138FC53",
            Convert.ToHexString(await SHA256.HashDataAsync(stream)));
    }

    [Fact]
    public async Task Sessions_AreWindowOwned_ReadOnly_AndReleaseReadersOnClose()
    {
        using var fixture = new Fixture();
        string archive = Path.Combine(fixture.Directory.Path, "large.zip");
        using(var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using(var output = zip.CreateEntry("large.bin", CompressionLevel.NoCompression).Open())
            await output.WriteAsync(RandomNumberGenerator.GetBytes(4 * 1024 * 1024));
        var root = await fixture.OpenAsync(archive);
        var secondOwner = Guid.NewGuid();
        var second = await fixture.Provider.MountAsync(secondOwner, archive, null, default);
        try
        {
            Assert.Empty(await fixture.Provider.GetRootsAsync());
            var file = Assert.Single(await fixture.Provider.GetChildrenAsync(root));
            Assert.False((await fixture.Provider.DeleteAsync(root)).Success);
            Assert.False((await fixture.Provider.RenameAsync(file.Location, "../x")).Success);
            Assert.False((await fixture.Provider.CreateContainerAsync(root)).Success);
            await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Provider.OpenRawWriteAsync(file.Location, true));
            await using var stream = await fixture.Provider.OpenRawReadAsync(file.Location);
            Assert.Equal(1, await stream.ReadAsync(new byte[1]));
            fixture.Provider.CloseOwner(fixture.Owner);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                while(await stream.ReadAsync(new byte[81920]) != 0) { }
            });
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => fixture.Provider.GetChildrenAsync(root));
            Assert.Single(await fixture.Provider.GetChildrenAsync(second));
        }
        finally { fixture.Provider.CloseOwner(secondOwner); }
        using var writable = File.Open(archive, FileMode.Open, FileAccess.Write, FileShare.None);
    }

    [Fact]
    public async Task CorruptCrc_ThrowsAtEnd_AndNeverPublishesCopiedFile()
    {
        using var fixture = new Fixture();
        string archive = Path.Combine(fixture.Directory.Path, "broken.zip");
        using(var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) Add(zip, "file.txt", "original payload");
        byte[] bytes = await File.ReadAllBytesAsync(archive);
        int payload = 30 + BitConverter.ToUInt16(bytes, 26) + BitConverter.ToUInt16(bytes, 28);
        bytes[payload] ^= 0x80;
        await File.WriteAllBytesAsync(archive, bytes);
        var root = await fixture.OpenAsync(archive);
        var file = Assert.Single(await fixture.Provider.GetChildrenAsync(root));
        string destination = Path.Combine(fixture.Directory.Path, "copied.txt");
        var result = await fixture.Provider.PullToLocalAsync(file.Location, new("local", destination));
        Assert.False(result.Success);
        Assert.False(File.Exists(destination));
        Assert.Empty(System.IO.Directory.GetDirectories(fixture.Directory.Path, ".cryptobook-extract-*"));
        await using var stream = await fixture.Provider.OpenRawReadAsync(file.Location);
        await Assert.ThrowsAsync<InvalidDataException>(() => stream.CopyToAsync(Stream.Null));
    }

    [Fact]
    public async Task CancelledCopyAndExistingDestination_LeaveSourceAndDestinationIntact()
    {
        using var fixture = new Fixture();
        string archive = Path.Combine(fixture.Directory.Path, "large.zip");
        using(var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using(var output = zip.CreateEntry("large.bin", CompressionLevel.NoCompression).Open())
            await output.WriteAsync(new byte[2 * 1024 * 1024]);
        var root = await fixture.OpenAsync(archive);
        var file = Assert.Single(await fixture.Provider.GetChildrenAsync(root));
        string outputPath = Path.Combine(fixture.Directory.Path, "result.bin");
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelOnProgress(cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Provider.PullToLocalAsync(
            file.Location, new("local", outputPath), progress, cancellation.Token));
        Assert.False(File.Exists(outputPath));
        Assert.Empty(System.IO.Directory.GetDirectories(fixture.Directory.Path, ".cryptobook-extract-*"));
        await File.WriteAllTextAsync(outputPath, "keep");
        Assert.False((await fixture.Provider.PullToLocalAsync(file.Location, new("local", outputPath))).Success);
        Assert.Equal("keep", await File.ReadAllTextAsync(outputPath));
    }

    [Fact]
    public async Task LargeTextPreview_IsBounded_MarksTruncation_AndStopsItsReader()
    {
        using var fixture = new Fixture();
        string archive = Path.Combine(fixture.Directory.Path, "preview.zip");
        using(var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            Add(zip, "large.txt", new string('A', 2 * 1024 * 1024));
        var root = await fixture.OpenAsync(archive);
        var metadata = Assert.Single(await fixture.Provider.GetChildrenAsync(root));
        var file = new FileItem { FullPath = metadata.Location.ToString(), Name = "large", Extension = ".txt", Size = metadata.Size };
        var preview = new FilePreviewService(new PreviewSource(fixture.Provider));
        var result = await preview.LoadAsync(file);
        Assert.Equal(FilePreviewKind.Text, result.Kind);
        Assert.StartsWith(new string('A', FilePreviewService.MaxTextBytes), result.Text);
        Assert.True(result.Text!.Length < FilePreviewService.MaxTextBytes + 256);
        Assert.Contains(CryptoBook.Infrastructure.LocalizationManager.GetString("Preview.Truncated"), result.Text);
        fixture.Provider.CloseOwner(fixture.Owner);
        using var editable = File.Open(archive, FileMode.Open, FileAccess.Write, FileShare.None);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("folder/CON.txt")]
    [InlineData("folder/file.txt:secret")]
    public async Task UnsafeEntry_IsRejectedBeforeMount(string name)
    {
        using var fixture = new Fixture();
        string archive = Path.Combine(fixture.Directory.Path, "unsafe.zip");
        using(var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) Add(zip, name, "unsafe");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.OpenAsync(archive));
        using var writable = File.Open(archive, FileMode.Open, FileAccess.Write, FileShare.None);
    }

    [Fact]
    public void HistoryAndSelection_PreserveArchiveLocations_AndCollapseSelectedDescendants()
    {
        string root = "archive://0123456789abcdef/";
        string folder = root + "Folder";
        string file = root + "Folder%5Cfile.txt";
        var history = new FileExplorerNavigationHistory();
        history.Commit(@"C:\Work", folder, FileExplorerNavigationMode.Standard);
        history.Commit(folder, file, FileExplorerNavigationMode.Standard);
        history.Commit(file, folder, FileExplorerNavigationMode.Back);
        Assert.Equal(file, history.ForwardPath);
        Assert.Equal(@"C:\Work", history.BackPath);
        // Container implementations are covered by visible navigation tests; opaque
        // identities must remain separate even when another provider has the same id.
        var selection = FileExplorerSelectionPolicy.NormalizeForOperation(new[]
        {
            new FileItem { FullPath = file }, new FileItem { FullPath = file.ToUpperInvariant() },
            new FileItem { FullPath = @"C:\Work\file.txt" }
        });
        Assert.Equal(2, selection.Count);
    }

    private static void Add(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.NoCompression).Open());
        writer.Write(text);
    }

    private sealed class Fixture: IDisposable
    {
        internal ArchiveTestDirectory Directory { get; } = new();
        internal Guid Owner { get; } = Guid.NewGuid();
        internal ArchiveService Service { get; }
        internal ArchiveStorageProvider Provider { get; }
        internal Fixture()
        {
            Service = Directory.CreateService();
            Provider = new ArchiveStorageProvider(Service, DispatchProxy.Create<ISystemItemCreateService, UnusedItems>());
        }
        internal Task<StorageLocation> OpenAsync(string path, string? password = null) => Provider.MountAsync(Owner, path, password, default);
        public void Dispose() { Provider.CloseOwner(Owner); Directory.Dispose(); }
    }
    public class UnusedItems: DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new NotSupportedException();
    }
    private sealed class CancelOnProgress(CancellationTokenSource cancellation): IProgressReporter
    {
        public void Report(double? progress, string? message = null) { if(progress > 0) cancellation.Cancel(); }
    }
    private sealed class PreviewSource(ArchiveStorageProvider provider): IFilePreviewContentSource
    {
        public bool HasDecryptionKey => false;
        public Task<bool> IsEncryptedAsync(IFileItem file, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Stream> OpenReadAsync(IFileItem file, CancellationToken cancellationToken = default) => provider.OpenRawReadAsync(file.Location, cancellationToken);
    }
}

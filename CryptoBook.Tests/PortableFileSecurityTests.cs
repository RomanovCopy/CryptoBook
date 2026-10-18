using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class PortableFileSecurityTests: IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(),
        "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
    private readonly WpdStorageProviderTests.BridgeStub bridge = new();
    private readonly StorageFacade storage;
    private readonly SecureFileProcessor processor;
    private readonly FileSecurityService service;

    public PortableFileSecurityTests()
    {
        Directory.CreateDirectory(directory);
        storage = new StorageFacade([new LocalStorageProvider(), new WpdStorageProvider(bridge)]);
        var key = new TestKey();
        processor = new SecureFileProcessor(
            new SecureFileV2Codec(key, new SecureFileV2Options()), new LegacySecureFileCodec(key));
        service = new FileSecurityService(new UnusedItemFactory(), processor, storageFacade: storage);
    }

    [Fact]
    public async Task ProtectedCopies_RemoteSourcesPreserveNamesContentAndExistingDestinations()
    {
        FileItem source = AddRemote("/Download/Отчёт.2026.txt", [0, 1, 2, 255]);
        AddRemote("/Download/Отчёт.2026.cbook", [99]);

        FileOperationBatchResult result = await service.CreateProtectedCopiesAsync(
            [source], Remote("/Download").ToString());

        Assert.True(result.Success, result.Results.FirstOrDefault()?.ErrorMessage);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(new byte[] { 0, 1, 2, 255 }, bridge.Content[("device", "/Download/Отчёт.2026.txt")]);
        Assert.Equal(new byte[] { 99 }, bridge.Content[("device", "/Download/Отчёт.2026.cbook")]);
        await AssertProtectedAsync(result.Results[0].AffectedPath!, [0, 1, 2, 255], ".txt");
        Assert.Equal(Remote("/Download/Отчёт.2026 (2).cbook").ToString(), result.Results[0].AffectedPath);
    }

    [Fact]
    public async Task EncryptAndDecryptReplacement_RoundTripsOnDevice()
    {
        byte[] content = [1, 2, 3, 254, 255];
        FileItem source = AddRemote("/Download/Текст.txt", content);
        FileOperationResult encrypted = await service.EncryptAsync(
            source, source.FullPath, EncryptionTargetMode.ReplaceSource);
        Assert.True(encrypted.Success, encrypted.ErrorMessage);
        Assert.False(bridge.Content.ContainsKey(("device", "/Download/Текст.txt")));
        await AssertProtectedAsync(encrypted.AffectedPath!, content, ".txt");

        FileItem protectedFile = Item(storage.Resolve(encrypted.AffectedPath!), "Текст.cbook");
        FileOperationResult decrypted = await service.DecryptAsync(
            protectedFile, protectedFile.FullPath, EncryptionTargetMode.ReplaceSource);
        Assert.True(decrypted.Success, decrypted.ErrorMessage);
        Assert.Equal(Remote("/Download/Текст.txt").ToString(), decrypted.AffectedPath);
        Assert.Equal(content, bridge.Content[("device", "/Download/Текст.txt")]);
        Assert.False(bridge.Content.ContainsKey(("device", "/Download/Текст.cbook")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProtectedCopies_WorkBetweenWindowsAndDevice(bool sourceIsRemote)
    {
        byte[] content = [9, 8, 7];
        string local = Path.Combine(directory, "Исходник.txt");
        await File.WriteAllBytesAsync(local, content);
        FileItem source = sourceIsRemote ? AddRemote("/Download/Исходник.txt", content)
            : Item(storage.Resolve(local), "Исходник.txt");
        string destination = sourceIsRemote ? directory : Remote("/Download").ToString();

        FileOperationBatchResult result = await service.CreateProtectedCopiesAsync([source], destination);

        Assert.True(result.Success, result.Results.FirstOrDefault()?.ErrorMessage);
        await AssertProtectedAsync(result.Results[0].AffectedPath!, content, ".txt");
        Assert.True(File.Exists(local));
        if(sourceIsRemote)
            Assert.Equal(content, bridge.Content[("device", "/Download/Исходник.txt")]);
    }

    [Fact]
    public async Task BatchReplacement_SkipsProtectedFilesAndDecryptsOnlyContainers()
    {
        FileItem first = AddRemote("/Download/first.2026.txt", [1]);
        FileItem second = AddRemote("/Download/second.txt", [2]);
        FileOperationBatchResult encrypted = await service.EncryptAsync([first, second]);
        Assert.True(encrypted.Success);
        Assert.Equal(2, encrypted.CompletedCount);
        FileItem[] protectedFiles = encrypted.Results.Select(result =>
            Item(storage.Resolve(result.AffectedPath!),
                Path.GetFileName(storage.FormatDisplayPath(storage.Resolve(result.AffectedPath!)))))
            .ToArray();
        FileOperationBatchResult skipped = await service.EncryptAsync(protectedFiles);
        Assert.True(skipped.Success);
        Assert.Equal(0, skipped.CompletedCount);
        Assert.Equal(2, skipped.SkippedCount);

        FileItem plain = AddRemote("/Download/plain.txt", [3]);
        FileOperationBatchResult decrypted = await service.DecryptAsync(
            [.. protectedFiles, plain], directory);
        Assert.True(decrypted.Success);
        Assert.Equal(2, decrypted.CompletedCount);
        Assert.Equal(1, decrypted.SkippedCount);
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(Path.Combine(directory, "first.2026.txt")));
        Assert.Equal(new byte[] { 2 }, await File.ReadAllBytesAsync(Path.Combine(directory, "second.txt")));
        Assert.Equal(new byte[] { 3 }, bridge.Content[("device", "/Download/plain.txt")]);
    }

    [Fact]
    public async Task CorruptedUpload_PreservesOriginalAndRemovesLocalWorkspace()
    {
        FileItem source = AddRemote("/Download/original.txt", [1, 2, 3]);
        bridge.CorruptCopiedFiles = true;
        var recording = new RecordingTransfer(new TransferEngine(storage));
        var security = new FileSecurityService(new UnusedItemFactory(), processor,
            storageFacade: storage, transferEngine: recording);

        FileOperationResult result = await security.EncryptAsync(
            source, source.FullPath, EncryptionTargetMode.ReplaceSource);

        Assert.False(result.Success);
        Assert.Equal(new byte[] { 1, 2, 3 }, bridge.Content[("device", "/Download/original.txt")]);
        Assert.NotNull(recording.Workspace);
        Assert.False(Directory.Exists(recording.Workspace));
    }

    [Fact]
    public async Task CancellationDuringUpload_PreservesOriginalAndRemovesLocalWorkspace()
    {
        FileItem source = AddRemote("/Download/original.txt", [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        var recording = new RecordingTransfer(new TransferEngine(storage), cancellation);
        var security = new FileSecurityService(new UnusedItemFactory(), processor,
            storageFacade: storage, transferEngine: recording);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => security.EncryptAsync(
            source, source.FullPath, EncryptionTargetMode.ReplaceSource, cancellationToken: cancellation.Token));

        Assert.Equal(new byte[] { 1, 2, 3 }, bridge.Content[("device", "/Download/original.txt")]);
        Assert.False(Directory.Exists(recording.Workspace));
    }

    [Fact]
    public async Task SourceChangedDuringUpload_IsPreserved()
    {
        FileItem source = AddRemote("/Download/original.txt", [1, 2, 3]);
        var recording = new RecordingTransfer(new TransferEngine(storage), afterMove: () =>
            bridge.Content[("device", "/Download/original.txt")] = [7, 8, 9]);
        var security = new FileSecurityService(new UnusedItemFactory(), processor,
            storageFacade: storage, transferEngine: recording);

        FileOperationResult result = await security.EncryptAsync(
            source, source.FullPath, EncryptionTargetMode.ReplaceSource);

        Assert.False(result.Success);
        Assert.Equal(new byte[] { 7, 8, 9 }, bridge.Content[("device", "/Download/original.txt")]);
        Assert.False(Directory.Exists(recording.Workspace));
    }

    private async Task AssertProtectedAsync(string path, byte[] expected, string extension)
    {
        StorageLocation location = storage.Resolve(path);
        string local = path;
        if(!location.IsLocal)
        {
            local = Path.Combine(directory, Guid.NewGuid() + ".cbook");
            Assert.True((await new TransferEngine(storage).CopyAsync(location, storage.Resolve(local))).Success);
        }
        await using DecryptedFileContent decrypted = await processor.DecryptFileContentAsync(local);
        Assert.Equal(extension, decrypted.OriginalExtension);
        using var content = new MemoryStream();
        await decrypted.Content.CopyToAsync(content);
        Assert.Equal(expected, content.ToArray());
    }

    private FileItem AddRemote(string path, byte[] content)
    {
        bridge.Entries[("device", path)] = new WpdStorageEntry(
            path, Path.GetFileName(path), false, content.Length, null);
        bridge.Content[("device", path)] = content;
        return Item(Remote(path), Path.GetFileName(path));
    }

    private static StorageLocation Remote(string path) => WpdLocatorCodec.Encode("device", path);
    private static FileItem Item(StorageLocation location, string name) => new()
    {
        FullPath = location.ToString(), Name = Path.GetFileNameWithoutExtension(name),
        Extension = Path.GetExtension(name)
    };

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private sealed class TestKey: IKeyProvider
    {
        public bool HasKey => true;
        public bool CanEncrypt => true;
        public void SetKey(ReadOnlySpan<char> password) { }
        public byte[] DeriveKey(byte[] salt) => new byte[32];
        public Task<byte[]> DeriveKeyAsync(ReadOnlyMemory<byte> salt, KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default) => Task.FromResult(new byte[32]);
        public void Clear() { }
    }

    private sealed class UnusedItemFactory: ISystemItemCreateService
    {
        public IDriveItem CreateRoot(string path) => throw new NotSupportedException();
        public IDirectoryItem CreateDirectory(string path, ISystemItem? parent) => throw new NotSupportedException();
        public IFileItem CreateFile(string path, ISystemItem? parent) => throw new NotSupportedException();
    }

    private sealed class RecordingTransfer(ITransferEngine inner, CancellationTokenSource? cancel = null,
        Action? afterMove = null): ITransferEngine
    {
        public string? Workspace { get; private set; }
        public Task<FileOperationResult> CopyAsync(StorageLocation source, StorageLocation destination,
            IProgressReporter? progress = null, CancellationToken cancellationToken = default) =>
            inner.CopyAsync(source, destination, progress, cancellationToken);
        public async Task<FileOperationResult> MoveAsync(StorageLocation source, StorageLocation destination,
            IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            Workspace = Path.GetDirectoryName(source.OpaqueId);
            cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            FileOperationResult result = await inner.MoveAsync(source, destination, progress, cancellationToken);
            afterMove?.Invoke();
            return result;
        }
    }
}

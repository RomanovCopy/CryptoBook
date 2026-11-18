using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ArchiveUpdatingTests
{
    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.SevenZip)]
    public async Task DeleteLastEntry_ProducesAValidEmptyArchive(ArchiveFormat format)
    {
        using var directory = new ArchiveTestDirectory();
        string input = Path.Combine(directory.Path, "input.txt");
        await File.WriteAllTextAsync(input, "content");
        string archive = Path.Combine(directory.Path, format == ArchiveFormat.Zip ? "empty.zip" : "empty.7z");
        var service = directory.CreateService();
        await service.CreateAsync([input], archive, format);
        await service.UpdateAsync(archive, [new(ArchiveChangeKind.Rename, "input.txt", "INPUT.txt")]);
        await service.UpdateAsync(archive, [new(ArchiveChangeKind.Delete, "INPUT.txt")]);
        await service.TestAsync(archive);
        using var session = await service.OpenReadSessionAsync(archive, null, default);
        Assert.Empty(session.Entries);
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip, null)]
    [InlineData(ArchiveFormat.SevenZip, null)]
    [InlineData(ArchiveFormat.Zip, "secret")]
    [InlineData(ArchiveFormat.SevenZip, "Пароль 🔐")]
    public async Task Update_RebuildsAndVerifies_AllChangesAndEncryption(ArchiveFormat format, string? password)
    {
        using var directory = new ArchiveTestDirectory();
        string folder = Directory.CreateDirectory(Path.Combine(directory.Path, "Книга")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "original.txt"), "original");
        await File.WriteAllTextAsync(Path.Combine(folder, "rename.txt"), "keep");
        await File.WriteAllTextAsync(Path.Combine(folder, "delete.txt"), "remove");
        string archive = Path.Combine(directory.Path, "a" + (format == ArchiveFormat.Zip ? ".zip" : ".7z"));
        var service = directory.CreateService();
        await service.CreateAsync([folder], archive, format, password);
        string replacement = Path.Combine(directory.Path, "replacement.txt");
        await File.WriteAllTextAsync(replacement, "изменено 🔐");
        List<string> statuses = [];
        await service.UpdateAsync(archive,
        [
            new(ArchiveChangeKind.Put, "Книга/original.txt", replacement, true),
            new(ArchiveChangeKind.Put, "новый.txt", replacement),
            new(ArchiveChangeKind.Rename, "Книга/rename.txt", "Книга/переименован.txt"),
            new(ArchiveChangeKind.Delete, "Книга/delete.txt"),
            new(ArchiveChangeKind.CreateDirectory, "пустая")
        ], password, progress: new Callback((_, info) => { if(info is not null) statuses.Add(info); }));
        Assert.Contains(ArchivePathPolicy.Text("Rebuilding"), statuses);
        Assert.DoesNotContain(ArchivePathPolicy.Text("Creating"), statuses);
        using(var session = await service.OpenReadSessionAsync(archive, password, default))
            Assert.All(session.Entries.Where(entry => !entry.IsDirectory), entry => Assert.Equal(password is not null, entry.IsEncrypted));
        string output = Path.Combine(directory.Path, "output");
        await service.ExtractAsync(archive, output, password);
        Assert.Equal("изменено 🔐", await File.ReadAllTextAsync(Path.Combine(output, "Книга/original.txt")));
        Assert.Equal("изменено 🔐", await File.ReadAllTextAsync(Path.Combine(output, "новый.txt")));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(output, "Книга/переименован.txt")));
        Assert.False(File.Exists(Path.Combine(output, "Книга/delete.txt")));
        Assert.True(Directory.Exists(Path.Combine(output, "пустая")));
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path, ".cryptobook-*"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("C:/escape")]
    [InlineData("folder/file:ads")]
    public async Task UnsafeUpdate_LeavesOriginalIntact(string name)
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path);
        byte[] before = await File.ReadAllBytesAsync(archive);
        await Assert.ThrowsAsync<InvalidDataException>(() => directory.CreateService().UpdateAsync(archive,
            [new(ArchiveChangeKind.CreateDirectory, name)]));
        Assert.Equal(before, await File.ReadAllBytesAsync(archive));
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path, ".cryptobook-*"));
    }

    [Fact]
    public async Task CancellationAndStaleVersion_LeaveOriginalIntact()
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path);
        byte[] before = await File.ReadAllBytesAsync(archive);
        var service = directory.CreateService();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UpdateAsync(archive,
            [new(ArchiveChangeKind.Delete, "file.txt")], progress: new Callback((value, _) =>
            { if(value > 0) cancellation.Cancel(); }), cancellationToken: cancellation.Token));
        await Assert.ThrowsAsync<IOException>(() => service.UpdateAsync(archive,
            [new(ArchiveChangeKind.Delete, "file.txt")], expectedHash: new string('0', 64)));
        Assert.Equal(before, await File.ReadAllBytesAsync(archive));
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path, ".cryptobook-*"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DamagedRebuildOrLockedCommit_DoesNotReplaceOriginal(bool lockCommit)
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path);
        byte[] before = await File.ReadAllBytesAsync(archive);
        FileStream? blocker = null;
        bool injected = false;
        try
        {
            var exception = await Record.ExceptionAsync(() => directory.CreateService().UpdateAsync(archive,
                [new(ArchiveChangeKind.CreateDirectory, "new")], progress: new Callback((_, info) =>
                {
                    if(injected || info != ArchivePathPolicy.Text("Verifying")) return;
                    injected = true;
                    if(lockCommit) blocker = File.Open(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
                    else File.WriteAllBytes(Directory.GetFiles(directory.Path, "updated.zip", SearchOption.AllDirectories).Single(), [1, 2, 3]);
                })));
            Assert.NotNull(exception);
            if(lockCommit) Assert.IsAssignableFrom<IOException>(exception);
            else Assert.IsType<InvalidDataException>(exception);
        }
        finally { blocker?.Dispose(); }
        Assert.True(injected);
        Assert.Equal(before, await File.ReadAllBytesAsync(archive));
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path, ".cryptobook-*"));
    }

    [Fact]
    public async Task Provider_RefreshesOtherOwners_AndRejectsStaleDocumentSave()
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path);
        var service = directory.CreateService();
        var provider = new ArchiveStorageProvider(service, DispatchProxy.Create<ISystemItemCreateService, UnusedItems>());
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        try
        {
            var root = await provider.MountAsync(first, archive, null, default);
            var other = await provider.MountAsync(second, archive, null, default);
            var file = Assert.Single(await provider.GetChildrenAsync(root));
            string hash = await provider.GetSourceHashAsync(file.Location, default);
            Assert.True((await provider.RenameAsync(file.Location, "renamed.txt")).Success);
            Assert.Equal("renamed.txt", Assert.Single(await provider.GetChildrenAsync(other)).Name);
            Assert.True((await provider.CreateContainerAsync(provider.GetChild(root, "empty"))).Success);
            string input = Path.Combine(directory.Path, "input.txt");
            await File.WriteAllTextAsync(input, "do not save");
            await Assert.ThrowsAsync<IOException>(() => provider.ReplaceDocumentAsync(
                provider.GetChild(root, "renamed.txt"), input, hash, new Callback((_, _) => { }), default));
            Assert.Equal(2, (await provider.GetChildrenAsync(root)).Count);
        }
        finally { provider.CloseOwner(first); provider.CloseOwner(second); }
        using var writable = File.Open(archive, FileMode.Open, FileAccess.Write, FileShare.None);
    }

    [Fact]
    public async Task SplitArchive_BrowsesAndExtracts_LocksAllVolumes_AndRejectsMissingVolume()
    {
        using var directory = new ArchiveTestDirectory();
        string input = Path.Combine(directory.Path, "random.bin");
        byte[] payload = RandomNumberGenerator.GetBytes(12 * 1024);
        await File.WriteAllBytesAsync(input, payload);
        var engine = new SevenZipArchiveEngine(Path.Combine(directory.Path, "engine"));
        string archive = Path.Combine(directory.Path, "volumes.7z");
        await engine.RunAsync(["a", "-t7z", "-v2k", "-bso0", archive, input], null, null, default);
        string first = archive + ".001";
        var service = directory.CreateService();
        using(var session = await service.OpenReadSessionAsync(first, null, default))
        {
            Assert.Single(session.Entries);
            Assert.Throws<IOException>(() => File.Open(archive + ".002", FileMode.Open, FileAccess.Write, FileShare.None));
            await using var read = session.OpenRead(session.Entries[0], default);
            using var memory = new MemoryStream();
            await read.CopyToAsync(memory);
            Assert.Equal(payload, memory.ToArray());
        }
        await service.ExtractAsync(first, Path.Combine(directory.Path, "out"));
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(directory.Path, "out/random.bin")));
        File.Delete(archive + ".002");
        await Assert.ThrowsAnyAsync<Exception>(() => service.ExtractAsync(first, Path.Combine(directory.Path, "missing")));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "missing")));
    }

    private static string CreateZip(string root)
    {
        string archive = Path.Combine(root, "test.zip");
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
        using var writer = new StreamWriter(zip.CreateEntry("file.txt").Open());
        writer.Write("original");
        return archive;
    }
    public class UnusedItems: DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException();
    }
    private sealed class Callback(Action<double?, string?> callback): IProgressReporter
    {
        public void Report(double? value, string? currentInfo = null) => callback(value, currentInfo);
    }
}

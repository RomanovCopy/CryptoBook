using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ArchiveServiceTests
{
    [Theory]
    [InlineData(ArchiveFormat.Zip, null)]
    [InlineData(ArchiveFormat.SevenZip, null)]
    [InlineData(ArchiveFormat.Zip, "password with spaces")]
    [InlineData(ArchiveFormat.SevenZip, "пароль 🔐 with spaces")]
    public async Task RoundTrip_PreservesUnicodeNestedHiddenAndEmptyEntries(ArchiveFormat format, string? password)
    {
        using var directory = new ArchiveTestDirectory();
        var service = directory.CreateService();
        string input = Directory.CreateDirectory(Path.Combine(directory.Path, "Исходная_папка")).FullName;
        Directory.CreateDirectory(Path.Combine(input, "пустая_папка"));
        Directory.CreateDirectory(Path.Combine(input, "вложенная"));
        await File.WriteAllTextAsync(Path.Combine(input, "вложенная", "документ.txt"), "Привет!\nТекст 🔐");
        await File.WriteAllBytesAsync(Path.Combine(input, "данные.bin"), [0, 1, 255, 100]);
        await File.WriteAllBytesAsync(Path.Combine(input, "пустой.txt"), []);
        string hidden = Path.Combine(input, ".hidden");
        await File.WriteAllTextAsync(hidden, "hidden content");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        string archive = Path.Combine(directory.Path, format == ArchiveFormat.Zip ? "проверка.zip" : "проверка.7z");
        string output = Path.Combine(directory.Path, "Распаковано");
        ArchiveOperationResult created = await service.CreateAsync([input], archive, format, password);
        Assert.Equal(4, created.FileCount);
        Assert.True(File.Exists(archive));
        await service.TestAsync(archive, password);
        ArchiveOperationResult extracted = await service.ExtractAsync(archive, output, password);
        Assert.Equal(created.TotalBytes, extracted.TotalBytes);
        Assert.Equal(4, extracted.FileCount);
        string restored = Path.Combine(output, "Исходная_папка");
        Assert.True(Directory.Exists(Path.Combine(restored, "пустая_папка")));
        foreach(string file in Directory.GetFiles(input, "*", SearchOption.AllDirectories))
            Assert.Equal(await File.ReadAllBytesAsync(file),
                await File.ReadAllBytesAsync(Path.Combine(restored, Path.GetRelativePath(input, file))));
        AssertNoWorkspaces(directory.Path);
    }

    [Fact]
    public async Task Create_UsesOnlySelectionAndRemovesOverlappingSources()
    {
        using var directory = new ArchiveTestDirectory();
        string selected = Directory.CreateDirectory(Path.Combine(directory.Path, "selected")).FullName;
        string child = Path.Combine(selected, "a.txt");
        await File.WriteAllTextAsync(child, "selected");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "secret.txt"), "must not be included");
        string archive = Path.Combine(directory.Path, "selected.zip");
        await directory.CreateService().CreateAsync([selected, child, selected], archive, ArchiveFormat.Zip);
        using var zip = ZipFile.OpenRead(archive);
        Assert.Single(zip.Entries, entry => entry.Length != 0);
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains("secret", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Rar.rar")]
    [InlineData("Rar5.rar")]
    [InlineData("Rar.solid.rar")]
    [InlineData("Rar5.solid.rar")]
    public async Task ExtractAndTest_SupportRealRarFixtures(string name)
    {
        using var directory = new ArchiveTestDirectory();
        string fixture = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Archives", name);
        var service = directory.CreateService();
        await service.TestAsync(fixture);
        var result = await service.ExtractAsync(fixture, Path.Combine(directory.Path, "result"));
        Assert.Equal(3, result.FileCount);
        Assert.True(result.TotalBytes > 0);
        Assert.Equal(result.TotalBytes, Directory.GetFiles(result.OutputPath, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length));
        var expected = new Dictionary<string, string>
        {
            ["exe/test.exe"] = "8557928804F57ECC340B3BB38B095A3607474EC8DEB0076F316FCFE02B562106",
            ["jpg/test.jpg"] = "B251C7501FB0F55DD4A92FEABE0A6F5733BC40A02679498155FAE9B30138FC53",
            ["тест.txt"] = "4D581D93D369F6E1C9B295FF38D82DABD577F927DFAF0C35818C015C85E322D9"
        };
        foreach(var (relative, hash) in expected)
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(
                Path.Combine(result.OutputPath, relative)))));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("a/../../outside.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/outside.txt")]
    [InlineData("file.txt:stream")]
    [InlineData("CON.txt")]
    [InlineData("folder/NUL")]
    [InlineData("trailing./file.txt")]
    [InlineData("trailing /file.txt")]
    [InlineData("a//file.txt")]
    [InlineData("file\nname.txt")]
    public async Task Extract_RejectsUnsafeArchiveNamesBeforeCreatingDestination(string name)
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path, [(name, "unsafe")]);
        string output = Path.Combine(directory.Path, "output");
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => directory.CreateService().ExtractAsync(archive, output));
        Assert.False(Directory.Exists(output));
        Assert.False(File.Exists(Path.Combine(directory.Path, "outside.txt")));
        AssertNoWorkspaces(directory.Path);
    }

    [Theory]
    [InlineData("A.txt", "a.txt")]
    [InlineData("a.txt", "a.txt")]
    [InlineData("folder", "folder/file.txt")]
    public async Task Extract_RejectsWindowsPathCollisions(string first, string second)
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path, [(first, "first"), (second, "second")]);
        string output = Path.Combine(directory.Path, "output");
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => directory.CreateService().ExtractAsync(archive, output));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task Extract_RejectsSymbolicLinkEntry()
    {
        using var directory = new ArchiveTestDirectory();
        string archive = Path.Combine(directory.Path, "link.zip");
        using(var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("link");
            entry.ExternalAttributes = unchecked((int)0xA1FF0000);
            using var writer = new StreamWriter(entry.Open());
            writer.Write("../../outside");
        }
        // Mark the ZIP's creator OS as Unix so the symlink attributes are meaningful.
        byte[] bytes = await File.ReadAllBytesAsync(archive);
        int central = bytes.AsSpan().IndexOf(new byte[] { 0x50, 0x4B, 0x01, 0x02 });
        Assert.True(central >= 0);
        bytes[central + 5] = 3;
        await File.WriteAllBytesAsync(archive, bytes);
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => directory.CreateService()
            .ExtractAsync(archive, Path.Combine(directory.Path, "output")));
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(100, 1)]
    public async Task Extract_EnforcesSizeAndEntryLimits(long bytes, int entries)
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path, [("a.txt", "12345"), ("b.txt", "67890")]);
        string output = Path.Combine(directory.Path, "output");
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => directory.CreateService()
            .ExtractAsync(archive, output, limits: new ArchiveExtractionLimits(bytes, entries)));
        Assert.False(Directory.Exists(output));
        AssertNoWorkspaces(directory.Path);
    }

    [Fact]
    public async Task Extract_DoesNotOverwriteExistingFolderOrFile()
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path, [("a.txt", "new")]);
        string output = Directory.CreateDirectory(Path.Combine(directory.Path, "output")).FullName;
        string existing = Path.Combine(output, "a.txt");
        await File.WriteAllTextAsync(existing, "original");
        await Assert.ThrowsAsync<IOException>(() => directory.CreateService().ExtractAsync(archive, output));
        Assert.Equal("original", await File.ReadAllTextAsync(existing));
        await Assert.ThrowsAsync<IOException>(() => directory.CreateService().ExtractAsync(archive, existing));
        Assert.Equal("original", await File.ReadAllTextAsync(existing));
    }

    [Fact]
    public async Task Create_RejectsExistingTargetAndTargetInsideSource()
    {
        using var directory = new ArchiveTestDirectory();
        string input = Directory.CreateDirectory(Path.Combine(directory.Path, "input")).FullName;
        await File.WriteAllTextAsync(Path.Combine(input, "a.txt"), "content");
        string existing = Path.Combine(directory.Path, "existing.zip");
        await File.WriteAllTextAsync(existing, "original");
        var service = directory.CreateService();
        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync([input], existing, ArchiveFormat.Zip));
        Assert.Equal("original", await File.ReadAllTextAsync(existing));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync([input], Path.Combine(input, "bad.zip"), ArchiveFormat.Zip));
        AssertNoWorkspaces(directory.Path);
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.SevenZip)]
    public async Task EncryptedArchive_RejectsWrongOrMissingPasswordWithoutPublishing(ArchiveFormat format)
    {
        using var directory = new ArchiveTestDirectory();
        string input = Path.Combine(directory.Path, "input.txt");
        await File.WriteAllTextAsync(input, "confidential");
        string archive = Path.Combine(directory.Path, format == ArchiveFormat.Zip ? "secret.zip" : "secret.7z");
        var service = directory.CreateService();
        await service.CreateAsync([input], archive, format, "correct password");
        foreach(string? password in new string?[] { null, "wrong" })
        {
            await Assert.ThrowsAnyAsync<InvalidDataException>(() => service.TestAsync(archive, password));
            string output = Path.Combine(directory.Path, "output");
            await Assert.ThrowsAnyAsync<InvalidDataException>(() => service.ExtractAsync(archive, output, password));
            Assert.False(Directory.Exists(output));
        }
        AssertNoWorkspaces(directory.Path);
    }

    [Fact]
    public async Task CorruptedPayload_FailsIntegrityAndExtractionWithoutPublishing()
    {
        using var directory = new ArchiveTestDirectory();
        const string content = "Unique payload to corrupt for CRC regression";
        string archive = CreateZip(directory.Path, [("file.txt", content)]);
        byte[] bytes = await File.ReadAllBytesAsync(archive);
        int index = bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(content));
        Assert.True(index > 0);
        bytes[index] ^= 0x20;
        await File.WriteAllBytesAsync(archive, bytes);
        var service = directory.CreateService();
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => service.TestAsync(archive));
        string output = Path.Combine(directory.Path, "output");
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => service.ExtractAsync(archive, output));
        Assert.False(Directory.Exists(output));
        AssertNoWorkspaces(directory.Path);
    }

    [Fact]
    public async Task CancelDuringExtraction_RemovesPartialDataAndPreservesArchive()
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path, [("large.txt", new string('x', 500_000))]);
        byte[] original = await File.ReadAllBytesAsync(archive);
        using var cancellation = new CancellationTokenSource();
        var reporter = new CallbackProgress((value, _) => { if(value is > 0 and < 1) cancellation.Cancel(); });
        string output = Path.Combine(directory.Path, "output");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => directory.CreateService().ExtractAsync(
            archive, output, progress: reporter, cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(output));
        Assert.Equal(original, await File.ReadAllBytesAsync(archive));
        AssertNoWorkspaces(directory.Path);
    }

    [Fact]
    public async Task CancelDuringNativeCompression_KillsProcessAndRemovesPartialArchive()
    {
        using var directory = new ArchiveTestDirectory();
        string input = Path.Combine(directory.Path, "input.bin");
        await File.WriteAllBytesAsync(input, RandomNumberGenerator.GetBytes(4 * 1024 * 1024));
        using var cancellation = new CancellationTokenSource();
        var reporter = new CallbackProgress((value, _) => { if(value.HasValue) cancellation.Cancel(); });
        string output = Path.Combine(directory.Path, "output.7z");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => directory.CreateService().CreateAsync(
            [input], output, ArchiveFormat.SevenZip, progress: reporter, cancellationToken: cancellation.Token));
        Assert.False(File.Exists(output));
        Assert.Equal(4 * 1024 * 1024, new FileInfo(input).Length);
        AssertNoWorkspaces(directory.Path);
    }

    [Fact]
    public async Task RuntimeIntegrityCheck_RejectsModifiedExecutable()
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path, [("a.txt", "content")]);
        var service = directory.CreateService();
        await service.TestAsync(archive);
        string runtime = Path.Combine(directory.Path, "engine", "7z.exe");
        await ArchiveTestDirectory.RetryAsync(() => File.WriteAllTextAsync(runtime, "tampered"));
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => service.TestAsync(archive));
    }

    [Theory]
    [InlineData("android://device/file.zip")]
    [InlineData("wpd://device/file.zip")]
    [InlineData("\\\\server\\share\\file.zip")]
    [InlineData("relative.zip")]
    public async Task ArchiveOperations_RejectNonLocalPaths(string path)
    {
        using var directory = new ArchiveTestDirectory();
        await Assert.ThrowsAsync<ArgumentException>(() => directory.CreateService().TestAsync(path));
    }

    [Fact]
    public async Task Create_PreservesSiblingRelativePathsAndSpecialCharacters()
    {
        using var directory = new ArchiveTestDirectory();
        string a = Directory.CreateDirectory(Path.Combine(directory.Path, "a")).FullName;
        string b = Directory.CreateDirectory(Path.Combine(directory.Path, "b")).FullName;
        string first = Path.Combine(a, "@first.txt");
        string second = Path.Combine(b, "-second.txt");
        await File.WriteAllTextAsync(first, "A");
        await File.WriteAllTextAsync(second, "B");
        string archive = Path.Combine(directory.Path, "special.7z");
        var service = directory.CreateService();
        await service.CreateAsync([first, second], archive, ArchiveFormat.SevenZip);
        string output = Path.Combine(directory.Path, "output");
        await service.ExtractAsync(archive, output);
        Assert.Equal("A", await File.ReadAllTextAsync(Path.Combine(output, "a", "@first.txt")));
        Assert.Equal("B", await File.ReadAllTextAsync(Path.Combine(output, "b", "-second.txt")));
    }

    [Fact]
    public async Task CreateZip_WithUnicodePassword_ReportsFormatLimitationBeforeWriting()
    {
        using var directory = new ArchiveTestDirectory();
        string input = Path.Combine(directory.Path, "input.txt");
        await File.WriteAllTextAsync(input, "data");
        await Assert.ThrowsAsync<ArgumentException>(() => directory.CreateService().CreateAsync(
            [input], Path.Combine(directory.Path, "output.zip"), ArchiveFormat.Zip, "пароль"));
        Assert.False(File.Exists(Path.Combine(directory.Path, "output.zip")));
        AssertNoWorkspaces(directory.Path);
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.SevenZip)]
    public async Task EmptyFolder_RoundTripsWithoutFileData(ArchiveFormat format)
    {
        using var directory = new ArchiveTestDirectory();
        string input = Directory.CreateDirectory(Path.Combine(directory.Path, "empty_folder")).FullName;
        string archive = Path.Combine(directory.Path, format == ArchiveFormat.Zip ? "empty.zip" : "empty.7z");
        var service = directory.CreateService();
        await service.CreateAsync([input], archive, format);
        string output = Path.Combine(directory.Path, "output");
        var result = await service.ExtractAsync(archive, output);
        Assert.Equal(0, result.FileCount);
        Assert.True(Directory.Exists(Path.Combine(output, "empty_folder")));
    }

    [Fact]
    public async Task Extract_RetriesBriefDirectoryLockBeforePublishing()
    {
        using var directory = new ArchiveTestDirectory();
        string archive = CreateZip(directory.Path, [("file.txt", new string('a', 100_000))]);
        SafeFileHandle? handle = null;
        Task? release = null;
        var reporter = new CallbackProgress((value, _) =>
        {
            if(value is > 0 && handle is null)
            {
                string staging = Directory.GetDirectories(directory.Path, ".cryptobook-extract-*").Single();
                handle = CreateFile(staging, 0, 3, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
                Assert.False(handle.IsInvalid);
                release = Task.Run(async () => { await Task.Delay(200); handle.Dispose(); });
            }
        });
        string output = Path.Combine(directory.Path, "output");
        try
        {
            await directory.CreateService().ExtractAsync(archive, output, progress: reporter);
            Assert.True(File.Exists(Path.Combine(output, "file.txt")));
            Assert.NotNull(release);
            await release;
            AssertNoWorkspaces(directory.Path);
        }
        finally { handle?.Dispose(); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    private static string CreateZip(string root, (string Name, string Content)[] entries)
    {
        string archive = Path.Combine(root, "test.zip");
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
        foreach(var (name, content) in entries)
        {
            using var stream = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
            stream.Write(Encoding.UTF8.GetBytes(content));
        }
        return archive;
    }

    private static void AssertNoWorkspaces(string root) =>
        Assert.Empty(Directory.GetFileSystemEntries(root, ".cryptobook-*"));

    private sealed class CallbackProgress(Action<double?, string?> callback): IProgressReporter
    {
        public void Report(double? value, string? currentInfo = null) => callback(value, currentInfo);
    }
}

internal sealed class ArchiveTestDirectory: IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cryptobook-archives-" + Guid.NewGuid().ToString("N"));
    public ArchiveTestDirectory() => Directory.CreateDirectory(Path);
    public ArchiveService CreateService() => new(new SevenZipArchiveEngine(System.IO.Path.Combine(Path, "engine")));
    public void Dispose() => RetryAsync(() =>
    {
        if(Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        return Task.CompletedTask;
    }).GetAwaiter().GetResult();

    internal static async Task RetryAsync(Func<Task> action)
    {
        for(int attempt = 0; ; attempt++)
        {
            try { await action(); return; }
            catch(Exception exception) when(attempt < 20 && exception is IOException or UnauthorizedAccessException)
            { await Task.Delay(50); }
        }
    }
}

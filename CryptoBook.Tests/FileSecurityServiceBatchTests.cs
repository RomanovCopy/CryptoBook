using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileSecurityServiceBatchTests: IDisposable
{
    private readonly string testDirectory = Path.Combine(
        Path.GetTempPath(),
        "CryptoBook.Tests",
        Guid.NewGuid().ToString("N"));

    public FileSecurityServiceBatchTests()
    {
        Directory.CreateDirectory(testDirectory);
    }

    [Fact]
    public async Task EncryptBatch_ProcessesSelectedFileAndDirectoryTree()
    {
        string topLevelFile = CreateFile("large.bin", new byte[9]);
        string selectedDirectory = Path.Combine(testDirectory, "selected");
        Directory.CreateDirectory(selectedDirectory);
        string nestedFile = Path.Combine(selectedDirectory, "nested.bin");
        await File.WriteAllBytesAsync(nestedFile, [0x2A]);

        var processor = new RecordingSecureFileProcessor();
        var progress = new ProgressRecorder();
        var service = CreateService(processor);
        ISystemItem[] sources =
        [
            CreateFileItem(topLevelFile),
            new DirectoryStub(selectedDirectory)
        ];

        FileOperationBatchResult result = await service.EncryptAsync(
            sources,
            progress);

        Assert.True(result.Success);
        Assert.Equal(2, result.CompletedCount);
        Assert.Equal(
            [topLevelFile, nestedFile],
            processor.EncryptedInputs);
        Assert.Contains(
            progress.Values,
            value => value is not null && Math.Abs(value.Value - 0.45) < 0.001);
        Assert.Equal(1.0, progress.Values[^1]);
    }

    [Fact]
    public async Task DecryptBatch_ReplacesEverySelectedEncryptedFile()
    {
        string first = CreateFile("first.cbook", [1, 2, 3]);
        string second = CreateFile("second.cbook", [4, 5, 6]);

        var processor = new RecordingSecureFileProcessor();
        var service = CreateService(processor);
        ISystemItem[] sources =
        [
            CreateFileItem(first),
            CreateFileItem(second)
        ];

        FileOperationBatchResult result = await service.DecryptAsync(sources);

        Assert.True(result.Success);
        Assert.Equal(2, result.CompletedCount);
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(
            Path.Combine(testDirectory, "first.txt")));
        Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(
            Path.Combine(testDirectory, "second.txt")));
        Assert.Equal([first, second], processor.DecryptedInputs);
    }

    [Fact]
    public async Task EncryptBatch_ContinuesAfterFailureAndReportsCompletedItems()
    {
        string first = CreateFile("first.bin", [1]);
        string second = CreateFile("second.bin", [2]);
        string third = CreateFile("third.bin", [3]);

        var processor = new RecordingSecureFileProcessor
        {
            EncryptFailurePath = second
        };
        var service = CreateService(processor);
        ISystemItem[] sources =
        [
            CreateFileItem(first),
            CreateFileItem(second),
            CreateFileItem(third)
        ];

        FileOperationBatchResult result = await service.EncryptAsync(sources);

        Assert.False(result.Success);
        Assert.Equal(2, result.CompletedCount);
        Assert.True(result.HasPartialChanges);
        Assert.Equal(3, result.Results.Count);
        Assert.Equal([first, second, third], processor.EncryptedInputs);
        Assert.Equal(second, result.Failure?.AffectedPath);
        Assert.Contains(second, result.Failure?.ErrorMessage);
    }

    [Fact]
    public async Task EncryptBatch_ContinuesWhenSourceCannotBeMeasured()
    {
        string first = CreateFile("measured-first.bin", [1]);
        string missing = Path.Combine(testDirectory, "missing.bin");
        string third = CreateFile("measured-third.bin", [3]);

        var processor = new RecordingSecureFileProcessor();
        var service = CreateService(processor);
        ISystemItem[] sources =
        [
            CreateFileItem(first),
            new FileItem
            {
                FullPath = missing,
                Name = Path.GetFileName(missing),
                RootDirectory = Path.GetPathRoot(missing) ?? string.Empty,
                Extension = Path.GetExtension(missing)
            },
            CreateFileItem(third)
        ];

        FileOperationBatchResult result = await service.EncryptAsync(sources);

        Assert.False(result.Success);
        Assert.Equal(2, result.CompletedCount);
        Assert.Equal(3, result.Results.Count);
        Assert.Equal([first, third], processor.EncryptedInputs);
        Assert.Equal(missing, result.Failure?.AffectedPath);
        Assert.Contains(missing, result.Failure?.ErrorMessage);
    }

    [Fact]
    public async Task EncryptBatch_ContinuesAfterFailureInsideDirectory()
    {
        string selectedDirectory = Path.Combine(testDirectory, "with-failure");
        Directory.CreateDirectory(selectedDirectory);
        string first = Path.Combine(selectedDirectory, "01.bin");
        string second = Path.Combine(selectedDirectory, "02.bin");
        string third = Path.Combine(selectedDirectory, "03.bin");
        await File.WriteAllBytesAsync(first, [1]);
        await File.WriteAllBytesAsync(second, [2]);
        await File.WriteAllBytesAsync(third, [3]);

        var processor = new RecordingSecureFileProcessor
        {
            EncryptFailurePath = second
        };
        var service = CreateService(processor);

        FileOperationBatchResult result = await service.EncryptAsync(
            [new DirectoryStub(selectedDirectory)]);

        Assert.False(result.Success);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal([first, second, third], processor.EncryptedInputs);
        Assert.Contains(second, result.Failure?.ErrorMessage);
    }

    [Fact]
    public async Task RealCodecBatch_RoundTripsTenFiles()
    {
        var expected = new Dictionary<string, byte[]>();
        var sources = new List<ISystemItem>();
        for(int index = 0; index < 10; index++)
        {
            byte[] content = Enumerable.Range(0, 1024 + index)
                .Select(value => (byte)(value + index))
                .ToArray();
            string path = CreateFile($"real-{index:D2}.txt", content);
            expected[path] = content;
            sources.Add(CreateFileItem(path));
        }

        FileSecurityService service = CreateRealService();
        string readOnlyPath = sources[3].FullPath;
        File.SetAttributes(
            readOnlyPath,
            File.GetAttributes(readOnlyPath) | FileAttributes.ReadOnly);

        try
        {
            FileOperationBatchResult encrypted = await service.EncryptAsync(sources);
            Assert.True(encrypted.Success);
            Assert.Equal(10, encrypted.CompletedCount);
            foreach(string path in expected.Keys)
                Assert.True(await new SecureFileValidator().HasCryptoBookHeaderAsync(path));
            Assert.True(
                (File.GetAttributes(readOnlyPath) & FileAttributes.ReadOnly) != 0);

            FileOperationBatchResult decrypted = await service.DecryptAsync(sources);
            Assert.True(decrypted.Success);
            Assert.Equal(10, decrypted.CompletedCount);
            foreach((string path, byte[] content) in expected)
                Assert.Equal(content, await File.ReadAllBytesAsync(path));
            Assert.True(
                (File.GetAttributes(readOnlyPath) & FileAttributes.ReadOnly) != 0);
        }
        finally
        {
            if(File.Exists(readOnlyPath))
            {
                File.SetAttributes(
                    readOnlyPath,
                    File.GetAttributes(readOnlyPath) & ~FileAttributes.ReadOnly);
            }
        }
    }

    private FileSecurityService CreateService(
        ISecureFileProcessor processor) =>
        new(new CreateServiceStub(), processor);

    private FileSecurityService CreateRealService()
    {
        var keyProvider = new FixedKeyProvider();
        var options = new SecureFileV2Options
        {
            KeyDerivation = new KeyDerivationParameters(
                Iterations: 1,
                MemorySizeKiB: 8 * 1024,
                DegreeOfParallelism: 1,
                OutputLength: 32),
            ChunkSize = 4096
        };
        var processor = new SecureFileProcessor(
            new SecureFileV2Codec(keyProvider, options),
            new LegacySecureFileCodec(keyProvider));
        return CreateService(processor);
    }

    private string CreateFile(string name, byte[] content)
    {
        string path = Path.Combine(testDirectory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static FileItem CreateFileItem(string path) => new()
    {
        FullPath = path,
        Name = Path.GetFileName(path),
        RootDirectory = Path.GetPathRoot(path) ?? string.Empty,
        Extension = Path.GetExtension(path),
        Size = new FileInfo(path).Length
    };

    public void Dispose()
    {
        if(Directory.Exists(testDirectory))
            Directory.Delete(testDirectory, recursive: true);
    }

    private sealed class ProgressRecorder: IProgressReporter
    {
        public List<double?> Values { get; } = [];

        public void Report(double? value, string? currentInfo = null) =>
            Values.Add(value);
    }

    private sealed class FixedKeyProvider: IKeyProvider
    {
        private readonly byte[] key = Enumerable.Range(1, 32)
            .Select(value => (byte)value)
            .ToArray();

        public bool HasKey => true;
        public void SetKey(ReadOnlySpan<char> password)
        {
        }
        public byte[] DeriveKey(byte[] salt) => key.ToArray();
        public Task<byte[]> DeriveKeyAsync(
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(key.ToArray());
        }
        public void Clear()
        {
        }
    }

    private sealed class RecordingSecureFileProcessor: ISecureFileProcessor
    {
        public List<string> EncryptedInputs { get; } = [];
        public List<string> DecryptedInputs { get; } = [];
        public string? EncryptFailurePath { get; init; }

        public Task EncryptFileAsync(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EncryptedInputs.Add(inputFile);
            if(string.Equals(
                inputFile,
                EncryptFailurePath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Expected batch failure.");
            }
            progress?.Report(0.5, inputFile);
            progress?.Report(1.0, inputFile);
            return Task.CompletedTask;
        }

        public async Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DecryptedInputs.Add(inputFile);
            progress?.Report(0.5, inputFile);
            await File.WriteAllBytesAsync(
                outputFile + ".txt",
                await File.ReadAllBytesAsync(inputFile, cancellationToken),
                cancellationToken);
            progress?.Report(1.0, inputFile);
        }

        public Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new NotSupportedException());

        public Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<DecryptedFileContent>(new NotSupportedException());

        public Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<Stream>(new NotSupportedException());
    }

    private sealed class CreateServiceStub: ISystemItemCreateService
    {
        public IDriveItem CreateRoot(string rootPath) =>
            throw new NotSupportedException();

        public IDirectoryItem CreateDirectory(
            string path,
            ISystemItem? parent) =>
            throw new NotSupportedException();

        public IFileItem CreateFile(string path, ISystemItem? parent) =>
            throw new NotSupportedException();
    }

    private sealed class DirectoryStub: IDirectoryItem
    {
        private readonly ReadOnlyObservableCollection<ISystemItem> children =
            new(new ObservableCollection<ISystemItem>());
        private readonly ReadOnlyObservableCollection<IContainerSystemItem> directories =
            new(new ObservableCollection<IContainerSystemItem>());

        public DirectoryStub(string path)
        {
            FullPath = path;
            Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            RootDirectory = Path.GetPathRoot(path) ?? string.Empty;
        }

        public string Name { get; set; }
        public string FullPath { get; set; }
        public string RootDirectory { get; set; }
        public long Size { get; set; }
        public bool IsEditing { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public ISystemItem? Parent { get; set; }
        public bool IsLoaded { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }
        public ReadOnlyObservableCollection<ISystemItem> Children => children;
        public ReadOnlyObservableCollection<IContainerSystemItem> DirectoryChildren => directories;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public Task<FileOperationResult> AddChildAsync(
            IEnumerable<ISystemItem> items,
            Func<ISystemItem, string> keySelector,
            CancellationToken ct = default) =>
            Task.FromResult(FileOperationResult.Ok());

        public Task<FileOperationResult> RenameChildAsync(
            ISystemItem item,
            string newName,
            CancellationToken ct = default) =>
            Task.FromResult(FileOperationResult.Ok());

        public Task<FileOperationResult> RemoveChildAsync(
            IEnumerable<ISystemItem> items,
            Func<ISystemItem, string> keySelector,
            CancellationToken ct = default) =>
            Task.FromResult(FileOperationResult.Ok());

        public Task<FileOperationResult> SortingAsync(
            SystemItemSortType sortType,
            int dir = 0,
            CancellationToken ct = default) =>
            Task.FromResult(FileOperationResult.Ok());

        public Task<FileOperationResult> ClearChildrenAsync() =>
            Task.FromResult(FileOperationResult.Ok());

        public Task SyncCollectionsAsync(
            IEnumerable<ISystemItem> source,
            Func<ISystemItem, string> keySelector,
            Action<ISystemItem, ISystemItem>? updateExisting,
            CancellationToken ct) =>
            Task.CompletedTask;
    }
}

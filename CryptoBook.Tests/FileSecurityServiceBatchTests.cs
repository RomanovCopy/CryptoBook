using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Validation;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

using Xunit;

namespace CryptoBook.Tests;

[Collection(WindowsRecycleBinCollection.Name)]
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
    public async Task DecryptBatch_SkipsFilesWithoutCryptoBookHeader()
    {
        string encrypted = CreateFile("protected.cbook", [1, 2, 3]);
        string plaintext = CreateFile("notes.txt", [4, 5, 6]);
        var processor = new RecordingSecureFileProcessor();
        var validator = new PredicateEncryptedValidator(path =>
            path.EndsWith(".cbook", StringComparison.OrdinalIgnoreCase));
        FileSecurityService service = CreateService(processor, validator);

        FileOperationBatchResult result = await service.DecryptAsync(
            [CreateFileItem(encrypted), CreateFileItem(plaintext)]);

        Assert.True(result.Success);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal([encrypted], processor.DecryptedInputs);
        Assert.True(File.Exists(plaintext));
        Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(plaintext));
    }

    [Fact]
    public async Task DecryptDirectory_SaveAsCopiesOnlyProtectedBranches()
    {
        string sourceDirectory = Path.Combine(testDirectory, "vault");
        string protectedDirectory = Path.Combine(sourceDirectory, "protected");
        string plainDirectory = Path.Combine(sourceDirectory, "plain-only");
        Directory.CreateDirectory(protectedDirectory);
        Directory.CreateDirectory(plainDirectory);
        string encrypted = Path.Combine(protectedDirectory, "item.cbook");
        string plaintext = Path.Combine(plainDirectory, "notes.txt");
        await File.WriteAllBytesAsync(encrypted, [1, 2, 3]);
        await File.WriteAllBytesAsync(plaintext, [4, 5, 6]);
        string destination = Path.Combine(testDirectory, "output");
        var processor = new RecordingSecureFileProcessor();
        FileSecurityService service = CreateService(
            processor,
            new PredicateEncryptedValidator(path =>
                path.EndsWith(".cbook", StringComparison.OrdinalIgnoreCase)));

        FileOperationBatchResult result = await service.DecryptAsync(
            [new DirectoryStub(sourceDirectory)],
            destination);

        Assert.True(result.Success);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(
            [1, 2, 3],
            await File.ReadAllBytesAsync(Path.Combine(
                destination,
                "vault",
                "protected",
                "item.txt")));
        Assert.False(Directory.Exists(Path.Combine(
            destination,
            "vault",
            "plain-only")));
        Assert.True(File.Exists(plaintext));
    }

    [Fact]
    public async Task DecryptBatch_SaveAsPreservesSourcesAndRenamesConflict()
    {
        string encrypted = CreateFile("report.cbook", [1, 2, 3]);
        string destination = Path.Combine(testDirectory, "decrypted");
        Directory.CreateDirectory(destination);
        string existing = Path.Combine(destination, "report.txt");
        await File.WriteAllBytesAsync(existing, [9, 9, 9]);
        var processor = new RecordingSecureFileProcessor();
        FileSecurityService service = CreateService(processor);

        FileOperationBatchResult result = await service.DecryptAsync(
            [CreateFileItem(encrypted)],
            destination);

        Assert.True(result.Success);
        Assert.True(File.Exists(encrypted));
        Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(existing));
        Assert.Equal(
            [1, 2, 3],
            await File.ReadAllBytesAsync(
                Path.Combine(destination, "report (2).txt")));
    }

    [Fact]
    public async Task DecryptSingle_ReplaceSourcePreservesConflictingPlaintext()
    {
        string encrypted = CreateFile("draft.cbook", [1, 2, 3]);
        string existing = CreateFile("draft.txt", [9, 9, 9]);
        var processor = new RecordingSecureFileProcessor();
        FileSecurityService service = CreateService(processor);

        FileOperationResult result = await service.DecryptAsync(
            CreateFileItem(encrypted),
            encrypted,
            EncryptionTargetMode.ReplaceSource);

        Assert.True(result.Success);
        Assert.False(File.Exists(encrypted));
        Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(existing));
        string uniqueCopy = Path.Combine(testDirectory, "draft (2).txt");
        Assert.Equal(uniqueCopy, result.AffectedPath);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(uniqueCopy));
    }

    [Fact]
    public async Task DecryptSingle_RejectsFileWithoutCryptoBookHeader()
    {
        string plaintext = CreateFile("plain.cbook", [1, 2, 3]);
        var processor = new RecordingSecureFileProcessor();
        FileSecurityService service = CreateService(
            processor,
            new PredicateEncryptedValidator(_ => false));

        FileOperationResult result = await service.DecryptAsync(
            CreateFileItem(plaintext),
            Path.Combine(testDirectory, "output"),
            EncryptionTargetMode.SaveAs);

        Assert.False(result.Success);
        Assert.Empty(processor.DecryptedInputs);
        Assert.True(File.Exists(plaintext));
    }

    [Fact]
    public async Task DecryptSingle_CancellationRemovesStagingPlaintext()
    {
        string encrypted = CreateFile("cancel.cbook", [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        var processor = new CancelingSecureFileProcessor(cancellation);
        FileSecurityService service = CreateService(processor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DecryptAsync(
                CreateFileItem(encrypted),
                encrypted,
                EncryptionTargetMode.ReplaceSource,
                cancellationToken: cancellation.Token));

        Assert.True(File.Exists(encrypted));
        Assert.Empty(Directory.GetDirectories(
            testDirectory,
            ".cryptobook-*"));
        Assert.False(File.Exists(Path.Combine(testDirectory, "cancel.txt")));
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
    public async Task ProtectedDirectory_RealCodecPreservesTreeAndDecryptsNestedCopies()
    {
        string source = Path.Combine(testDirectory, "source");
        string nested = Path.Combine(source, "one", "two", "три");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(Path.Combine(source, "empty"));
        var expected = new Dictionary<string, byte[]>
        {
            ["top.txt"] = [1, 2, 3],
            [Path.Combine("one", "two", "три", "report.2026.txt")] = [0, 255, 17],
            [Path.Combine("one", "two", "empty.bin")] = []
        };
        foreach(var (relative, content) in expected)
            await File.WriteAllBytesAsync(Path.Combine(source, relative), content);
        string hidden = Path.Combine(source, "top.txt");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        string destination = Path.Combine(testDirectory, "copies");
        FileSecurityService service = CreateRealService();
        var progress = new ProgressRecorder();

        FileOperationBatchResult encrypted = await service.CreateProtectedCopiesAsync(
            [new DirectoryStub(source)], destination, progress);

        Assert.True(encrypted.Success, encrypted.Failure?.ErrorMessage);
        Assert.Equal(3, encrypted.CompletedCount);
        Assert.Equal(1.0, progress.Values[^1]);
        string protectedRoot = Path.Combine(destination, "source");
        Assert.True(Directory.Exists(Path.Combine(protectedRoot, "empty")));
        foreach(var (relative, content) in expected)
        {
            Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(source, relative)));
            Assert.True(await new SecureFileValidator().HasCryptoBookHeaderAsync(
                Path.ChangeExtension(Path.Combine(protectedRoot, relative), ".cbook")));
        }

        string decryptedRoot = Path.Combine(testDirectory, "decrypted");
        FileOperationBatchResult decrypted = await service.DecryptAsync(
            [new DirectoryStub(protectedRoot)], decryptedRoot);

        Assert.True(decrypted.Success, decrypted.Failure?.ErrorMessage);
        Assert.Equal(3, decrypted.Results.Sum(result => result.ProcessedFileCount));
        foreach(var (relative, content) in expected)
            Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(decryptedRoot, "source", relative)));
        Assert.Equal(3, Directory.GetFiles(protectedRoot, "*", SearchOption.AllDirectories).Length);
        Assert.True(File.GetAttributes(hidden).HasFlag(FileAttributes.Hidden));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedAndDecryptedDirectories_CanBeRecycledImmediately(bool decrypt)
    {
        string source = Path.Combine(testDirectory, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        string original = Path.Combine(source, "nested", "report.txt");
        await File.WriteAllBytesAsync(original, [1, 2, 255]);
        FileSecurityService service = CreateRealService();
        string copies = Path.Combine(testDirectory, "copies");
        FileOperationBatchResult encrypted = await service.CreateProtectedCopiesAsync(
            [new DirectoryStub(source)], copies);
        Assert.True(encrypted.Success, encrypted.Failure?.ErrorMessage);
        string resultRoot = Path.Combine(copies, "source");
        if(decrypt)
        {
            string destination = Path.Combine(testDirectory, "decrypted");
            FileOperationBatchResult decrypted = await service.DecryptAsync(
                [new DirectoryStub(resultRoot)], destination);
            Assert.True(decrypted.Success, decrypted.Failure?.ErrorMessage);
            resultRoot = Path.Combine(destination, "source");
        }
        // FileShare.Delete сам по себе разрешает удаление даже при утечке чтения.
        // Эксклюзивное открытие проверяет именно освобождение файловых потоков.
        foreach(string path in Directory.EnumerateFiles(testDirectory, "*", SearchOption.AllDirectories))
        {
            using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        // Открытая/развёрнутая папка в FileExplorer также наблюдается за изменениями.
        Assert.False(File.GetAttributes(resultRoot).HasFlag(FileAttributes.ReadOnly));
        Assert.All(Directory.GetFileSystemEntries(resultRoot, "*", SearchOption.AllDirectories),
            path => Assert.False(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly)));
        using var monitoring = new DirectoryMonitoringService();
        Assert.True(monitoring.StartMonitoring(resultRoot, includeSubdirectories: true));
        var bin = new WindowsRecycleBinService();
        RecycleBinDeletionPlan plan = await bin.PlanDeletionAsync([resultRoot]);
        Assert.True(plan.Fits, "The disposable recycling test requires known free Recycle Bin capacity.");
        FileOperationBatchResult deleted = await bin.RecycleBatchAsync([resultRoot]);
        try
        {
            Assert.True(deleted.Success, deleted.Failure?.ErrorMessage);
            Assert.Equal(1, deleted.CompletedCount);
            Assert.Equal(0, deleted.PermanentlyDeletedCount);
            Assert.False(Directory.Exists(resultRoot));
            Assert.Equal([1, 2, 255], await File.ReadAllBytesAsync(original));
        }
        finally
        {
            monitoring.StopMonitoring(resultRoot);
            string? recycled = deleted.Results.FirstOrDefault(result => result.Success)?.AffectedPath;
            if(recycled is not null && Path.Exists(recycled) && !Path.Exists(resultRoot))
                await RecycleBinValidation.RestoreAsync(recycled, resultRoot);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedDirectory_DestinationInsideSourceDoesNotCaptureOutput(bool sameDirectory)
    {
        string source = Path.Combine(testDirectory, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        await File.WriteAllBytesAsync(Path.Combine(source, "nested", "original.txt"), [1]);
        string destination = sameDirectory ? source : Path.Combine(source, "output");
        Directory.CreateDirectory(destination);
        if(!sameDirectory)
            await File.WriteAllBytesAsync(Path.Combine(destination, "existing.txt"), [9]);
        var processor = new VerifyingSecureFileProcessor();
        FileSecurityService service = CreateService(processor, new PredicateEncryptedValidator(_ => false));

        FileOperationBatchResult result = await service.CreateProtectedCopiesAsync(
            [new DirectoryStub(source)], destination);

        Assert.True(result.Success, result.Failure?.ErrorMessage);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal([Path.Combine(source, "nested", "original.txt")], processor.EncryptedInputs);
        Assert.Equal([1], await File.ReadAllBytesAsync(Path.Combine(destination, "source", "nested", "original.cbook")));
        Assert.Equal([1], await File.ReadAllBytesAsync(Path.Combine(source, "nested", "original.txt")));
        if(!sameDirectory)
            Assert.Equal([9], await File.ReadAllBytesAsync(Path.Combine(destination, "existing.txt")));
    }

    [Fact]
    public async Task ProtectedDirectories_KeepConflictingRootsAndSkipProtectedFiles()
    {
        string destination = Path.Combine(testDirectory, "copies");
        Directory.CreateDirectory(Path.Combine(destination, "same"));
        string existing = Path.Combine(destination, "same", "keep.cbook");
        await File.WriteAllBytesAsync(existing, [99]);
        var sources = new List<ISystemItem>();
        foreach(string parent in new[] { "first", "second" })
        {
            string root = Path.Combine(testDirectory, parent, "same");
            Directory.CreateDirectory(root);
            await File.WriteAllBytesAsync(Path.Combine(root, "report.txt"), [1]);
            await File.WriteAllBytesAsync(Path.Combine(root, "report.rtf"), [2]);
            await File.WriteAllBytesAsync(Path.Combine(root, "protected.data"), [3]);
            sources.Add(new DirectoryStub(root));
        }
        FileSecurityService service = CreateService(new VerifyingSecureFileProcessor(),
            new PredicateEncryptedValidator(path => path.EndsWith("protected.data")));

        FileOperationBatchResult result = await service.CreateProtectedCopiesAsync(sources, destination);

        Assert.True(result.Success, result.Failure?.ErrorMessage);
        Assert.Equal(4, result.CompletedCount);
        Assert.Equal(2, result.SkippedCount);
        Assert.Equal([99], await File.ReadAllBytesAsync(existing));
        foreach(string name in new[] { "same (2)", "same (3)" })
        {
            string[] copies = Directory.GetFiles(Path.Combine(destination, name));
            Assert.Equal(2, copies.Length);
            Assert.Equal(new byte[] { 1, 2 }, copies.Select(path => File.ReadAllBytes(path)[0]).Order());
        }
        foreach(ISystemItem source in sources)
            Assert.Equal(3, Directory.GetFiles(source.FullPath).Length);
        Assert.Empty(Directory.GetFiles(destination, ".cryptobook-copy-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ProtectedDirectory_CancelDuringScanDoesNotCreateOutput()
    {
        string source = Path.Combine(testDirectory, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        string original = Path.Combine(source, "nested", "file.txt");
        await File.WriteAllBytesAsync(original, [1, 2, 3]);
        string destination = Path.Combine(testDirectory, "copies");
        using var cancellation = new CancellationTokenSource();
        var progress = new CallbackProgressReporter((_, path) =>
        {
            if(path == Path.Combine(source, "nested"))
                cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateRealService().CreateProtectedCopiesAsync(
                [new DirectoryStub(source)], destination, progress, cancellation.Token));

        Assert.False(Directory.Exists(destination));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(original));
    }

    [Fact]
    public async Task ProtectedDirectory_CancelAfterCompletedBranchKeepsSourcesAndReportsPartialCopies()
    {
        string source = Path.Combine(testDirectory, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        string first = Path.Combine(source, "first.txt");
        string second = Path.Combine(source, "nested", "second.txt");
        await File.WriteAllBytesAsync(first, [1]);
        await File.WriteAllBytesAsync(second, [2]);
        string destination = Path.Combine(testDirectory, "copies");
        using var cancellation = new CancellationTokenSource();
        var progress = new CallbackProgressReporter((value, path) =>
        {
            if(path == source && value is > 0 and < 1)
                cancellation.Cancel();
        });
        FileSecurityService service = CreateService(new VerifyingSecureFileProcessor(),
            new PredicateEncryptedValidator(_ => false));

        FileOperationBatchResult result = await service.CreateProtectedCopiesAsync(
            [new DirectoryStub(source)], destination, progress, cancellation.Token);

        Assert.True(result.Canceled);
        Assert.True(result.HasPartialChanges);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal([1], await File.ReadAllBytesAsync(first));
        Assert.Equal([2], await File.ReadAllBytesAsync(second));
        Assert.Equal([1], await File.ReadAllBytesAsync(Path.Combine(destination, "source", "first.cbook")));
        Assert.Single(Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(destination, ".cryptobook-copy-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ProtectedDirectory_FileFailureDoesNotPreventOtherCopies()
    {
        string source = Path.Combine(testDirectory, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        string failed = Path.Combine(source, "nested", "failed.txt");
        string good = Path.Combine(source, "nested", "good.txt");
        await File.WriteAllBytesAsync(failed, [1]);
        await File.WriteAllBytesAsync(good, [2]);
        string destination = Path.Combine(testDirectory, "copies");
        FileSecurityService service = CreateService(new VerifyingSecureFileProcessor
        {
            EncryptFailurePath = failed
        }, new PredicateEncryptedValidator(_ => false));

        FileOperationBatchResult result = await service.CreateProtectedCopiesAsync(
            [new DirectoryStub(source)], destination);

        Assert.False(result.Success);
        Assert.True(result.HasPartialChanges);
        Assert.Equal(1, result.CompletedCount);
        Assert.Contains(failed, result.Failure?.ErrorMessage);
        Assert.Equal([1], await File.ReadAllBytesAsync(failed));
        Assert.Equal([2], await File.ReadAllBytesAsync(good));
        Assert.Equal([2], await File.ReadAllBytesAsync(Path.Combine(destination, "source", "nested", "good.cbook")));
        Assert.Single(Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ProtectedLocalDirectory_CanBeCopiedToDeviceAndDecryptedBack()
    {
        string source = Path.Combine(testDirectory, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        string original = Path.Combine(source, "nested", "report.txt");
        await File.WriteAllBytesAsync(original, [1, 2, 255]);
        var bridge = new WpdStorageProviderTests.BridgeStub();
        bridge.Entries[("device", "/Download")] = new("/Download", "Download", true, 0, null);
        var storage = new StorageFacade([new LocalStorageProvider(), new WpdStorageProvider(bridge)]);
        FileSecurityService service = CreateRealService(storage);

        FileOperationBatchResult encrypted = await service.CreateProtectedCopiesAsync(
            [new DirectoryStub(source)], WpdLocatorCodec.Encode("device", "/Download").ToString());

        Assert.True(encrypted.Success, encrypted.Failure?.ErrorMessage);
        Assert.Equal(1, encrypted.CompletedCount);
        string protectedPath = Assert.Single(encrypted.Results).AffectedPath!;
        Assert.Equal(WpdLocatorCodec.Encode("device", "/Download/source/nested/report.cbook").ToString(), protectedPath);
        var protectedFile = new FileItem { FullPath = protectedPath, Name = "report", Extension = ".cbook" };
        string decrypted = Path.Combine(testDirectory, "decrypted");
        Directory.CreateDirectory(decrypted);
        FileOperationBatchResult result = await service.DecryptAsync([protectedFile], decrypted);

        Assert.True(result.Success, result.Failure?.ErrorMessage);
        Assert.Equal([1, 2, 255], await File.ReadAllBytesAsync(Path.Combine(decrypted, "report.txt")));
        Assert.Equal([1, 2, 255], await File.ReadAllBytesAsync(original));
        Assert.True(bridge.Content.ContainsKey(("device", "/Download/source/nested/report.cbook")));
    }

    [Fact]
    public async Task CreateProtectedCopies_PreservesSourcesAndUsesUniqueCbookNames()
    {
        string firstDirectory = Path.Combine(testDirectory, "first-source");
        string secondDirectory = Path.Combine(testDirectory, "second-source");
        string destination = Path.Combine(testDirectory, "protected");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        Directory.CreateDirectory(destination);
        string first = Path.Combine(firstDirectory, "report.txt");
        string second = Path.Combine(secondDirectory, "report.rtf");
        await File.WriteAllBytesAsync(first, [1, 2, 3]);
        await File.WriteAllBytesAsync(second, [4, 5, 6]);
        string existing = Path.Combine(destination, "report.cbook");
        await File.WriteAllBytesAsync(existing, [9, 9, 9]);

        var processor = new VerifyingSecureFileProcessor();
        FileSecurityService service = CreateService(
            processor,
            new PredicateEncryptedValidator(_ => false));

        FileOperationBatchResult result =
            await service.CreateProtectedCopiesAsync(
                [CreateFileItem(first), CreateFileItem(second)],
                destination);

        Assert.True(result.Success);
        Assert.Equal(2, result.CompletedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(first));
        Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(second));
        Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(existing));
        Assert.Equal(
            [1, 2, 3],
            await File.ReadAllBytesAsync(Path.Combine(
                destination,
                "report (2).cbook")));
        Assert.Equal(
            [4, 5, 6],
            await File.ReadAllBytesAsync(Path.Combine(
                destination,
                "report (3).cbook")));
        Assert.Empty(Directory.GetFiles(
            destination,
            ".cryptobook-copy-*.tmp"));
    }

    [Fact]
    public async Task CreateProtectedCopies_SkipsAlreadyProtectedSource()
    {
        string source = CreateFile("protected.data", [1, 2, 3]);
        string destination = Path.Combine(testDirectory, "copies");
        var processor = new VerifyingSecureFileProcessor();
        FileSecurityService service = CreateService(
            processor,
            new PredicateEncryptedValidator(path => path == source));

        FileOperationBatchResult result =
            await service.CreateProtectedCopiesAsync(
                [CreateFileItem(source)],
                destination);

        Assert.True(result.Success);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Empty(processor.EncryptedInputs);
        Assert.Empty(Directory.GetFiles(destination));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(source));
    }

    [Fact]
    public async Task CreateProtectedCopies_RemovesCopyWhenVerificationFails()
    {
        string source = CreateFile("draft.txt", [1, 2, 3]);
        string destination = Path.Combine(testDirectory, "copies");
        var processor = new VerifyingSecureFileProcessor
        {
            StoredExtensionOverride = ".wrong"
        };
        FileSecurityService service = CreateService(
            processor,
            new PredicateEncryptedValidator(_ => false));

        FileOperationBatchResult result =
            await service.CreateProtectedCopiesAsync(
                [CreateFileItem(source)],
                destination);

        Assert.False(result.Success);
        Assert.Equal(0, result.CompletedCount);
        Assert.Contains(
            "draft.txt",
            result.Failure?.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(destination, "draft.cbook")));
        Assert.Empty(Directory.GetFiles(
            destination,
            ".cryptobook-copy-*.tmp"));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(source));
    }

    [Fact]
    public async Task CreateProtectedCopies_RealCodecRoundTripsWithoutChangingSource()
    {
        byte[] expected = Enumerable.Range(0, 4097)
            .Select(value => (byte)value)
            .ToArray();
        string source = CreateFile("round-trip.txt", expected);
        string destination = Path.Combine(testDirectory, "protected");
        FileSecurityService service = CreateRealService();

        FileOperationBatchResult protectedResult =
            await service.CreateProtectedCopiesAsync(
                [CreateFileItem(source)],
                destination);

        Assert.True(protectedResult.Success);
        Assert.Equal(expected, await File.ReadAllBytesAsync(source));
        string protectedPath = Path.Combine(
            destination,
            "round-trip.cbook");
        Assert.True(await new SecureFileValidator()
            .HasCryptoBookHeaderAsync(protectedPath));

        string decryptedBasePath = Path.Combine(testDirectory, "published");
        FileOperationResult decrypted = await service.DecryptAsync(
            CreateFileItem(protectedPath),
            decryptedBasePath,
            EncryptionTargetMode.SaveAs);

        Assert.True(decrypted.Success);
        Assert.Equal(
            expected,
            await File.ReadAllBytesAsync(decryptedBasePath + ".txt"));
        Assert.True(File.Exists(protectedPath));
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
        ISecureFileProcessor processor,
        ISecureFileValidator? validator = null) =>
        new(
            new CreateServiceStub(),
            processor,
            secureFileValidator: validator ??
                new AlwaysEncryptedValidator());

    private FileSecurityService CreateRealService(IStorageFacade? storage = null)
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
        return new FileSecurityService(
            new CreateServiceStub(),
            processor,
            secureFileValidator: new SecureFileValidator(),
            storageFacade: storage);
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

    private sealed class CallbackProgressReporter(Action<double?, string?> report): IProgressReporter
    {
        public void Report(double? value, string? currentInfo = null) => report(value, currentInfo);
    }

    private sealed class FixedKeyProvider: IKeyProvider
    {
        private readonly byte[] key = Enumerable.Range(1, 32)
            .Select(value => (byte)value)
            .ToArray();

        public bool HasKey => true;
        public bool CanEncrypt => true;
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

    private sealed class VerifyingSecureFileProcessor: ISecureFileProcessor
    {
        private readonly Dictionary<string, string> storedExtensions =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> EncryptedInputs { get; } = [];
        public string? StoredExtensionOverride { get; init; }
        public string? EncryptFailurePath { get; init; }

        public async Task EncryptFileAsync(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            EncryptedInputs.Add(inputFile);
            if(inputFile == EncryptFailurePath)
                throw new IOException("Expected encryption failure.");
            storedExtensions[outputFile] = Path.GetExtension(inputFile);
            await File.WriteAllBytesAsync(
                outputFile,
                await File.ReadAllBytesAsync(inputFile, cancellationToken),
                cancellationToken);
            progress?.Report(1.0, inputFile);
        }

        public Task<string> ReadOriginalExtensionAsync(
            string inputFile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                StoredExtensionOverride ?? storedExtensions[inputFile]);
        }

        public Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class AlwaysEncryptedValidator: ISecureFileValidator
    {
        public Task<bool> HasCryptoBookHeaderAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }

    private sealed class PredicateEncryptedValidator:
        ISecureFileValidator
    {
        private readonly Func<string, bool> predicate;

        public PredicateEncryptedValidator(Func<string, bool> predicate)
        {
            this.predicate = predicate;
        }

        public Task<bool> HasCryptoBookHeaderAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(predicate(filePath));
        }
    }

    private sealed class CancelingSecureFileProcessor:
        ISecureFileProcessor
    {
        private readonly CancellationTokenSource cancellation;

        public CancelingSecureFileProcessor(
            CancellationTokenSource cancellation)
        {
            this.cancellation = cancellation;
        }

        public Task EncryptFileAsync(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            await File.WriteAllTextAsync(
                outputFile + ".txt",
                "temporary plaintext",
                CancellationToken.None);
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
        }

        public Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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

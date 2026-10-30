using Autofac;

using CryptoBook.DTO;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Windows;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileOperationCoordinatorTests
{
    [WpfFact]
    public void Startup_ResolvesCoordinatorAndFileExplorerModel()
    {
        var app = Application.Current ?? new Application();
        using IContainer container = new Startup().ConfigureServices(app);
        using ILifetimeScope scope = container.BeginLifetimeScope();

        Assert.NotNull(scope.Resolve<IFileOperationCoordinator>());
        Assert.NotNull(scope.Resolve<IFileConflictResolver>());
        Assert.NotNull(scope.Resolve<IFileExplorerModel>());
    }

    [Fact]
    public async Task TransferAsync_AggregatesProgressByTotalFileSize()
    {
        using var temp = new TempDirectory();
        string sourceDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "source")).FullName;
        string destinationDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "destination")).FullName;
        string small = Path.Combine(sourceDirectory, "small.bin");
        string large = Path.Combine(sourceDirectory, "large.bin");
        File.WriteAllBytes(small, new byte[100]);
        File.WriteAllBytes(large, new byte[300]);

        var manager = new FileManagerStub();
        var progress = new ProgressDialogStub();
        var coordinator = new FileOperationCoordinator(
            manager,
            progress,
            new ConflictResolverStub(FileConflictAction.Skip));

        FileOperationBatchResult result = await coordinator.TransferAsync(
            [small, large],
            destinationDirectory,
            FileTransferKind.Copy);

        Assert.True(result.Success);
        Assert.Equal(2, result.CompletedCount);
        Assert.Contains(progress.Values, value => Math.Abs(value - 0.125) < 0.0001);
        Assert.Contains(progress.Values, value => Math.Abs(value - 0.625) < 0.0001);
        Assert.Equal(1, progress.Values[^1], 6);
    }

    [Fact]
    public async Task TransferAsync_WaitsForViewSynchronizationBeforeProgressCompletes()
    {
        using var temp = new TempDirectory();
        string sourceDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "source")).FullName;
        string destinationDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "destination")).FullName;
        string source = Path.Combine(sourceDirectory, "data.bin");
        File.WriteAllBytes(source, new byte[16]);

        var progress = new ProgressDialogStub();
        var coordinator = new FileOperationCoordinator(
            new FileManagerStub(),
            progress,
            new ConflictResolverStub(FileConflictAction.Skip));
        bool synchronizationSawOpenProgress = false;

        FileOperationBatchResult result = await coordinator.TransferAsync(
            [source],
            destinationDirectory,
            FileTransferKind.Copy,
            synchronizeViewAsync: () =>
            {
                synchronizationSawOpenProgress = progress.IsOperationActive;
                return Task.CompletedTask;
            });

        Assert.True(result.Success);
        Assert.True(synchronizationSawOpenProgress);
        Assert.False(progress.IsOperationActive);
    }

    [Fact]
    public async Task TransferAsync_KeepBoth_UsesUniqueDestinationName()
    {
        using var temp = new TempDirectory();
        string sourceDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "source")).FullName;
        string destinationDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "destination")).FullName;
        string source = Path.Combine(sourceDirectory, "same.txt");
        File.WriteAllText(source, "new");
        File.WriteAllText(Path.Combine(destinationDirectory, "same.txt"), "old");

        var manager = new FileManagerStub();
        var coordinator = new FileOperationCoordinator(
            manager,
            new ProgressDialogStub(),
            new ConflictResolverStub(FileConflictAction.KeepBoth));

        FileOperationBatchResult result = await coordinator.TransferAsync(
            [source],
            destinationDirectory,
            FileTransferKind.Copy);

        Assert.True(result.Success);
        Assert.Equal(
            "new", File.ReadAllText(Path.Combine(destinationDirectory, "same - Copy.txt")));
        Assert.Empty(manager.DeletedPaths);
    }

    [Fact]
    public async Task TransferAsync_CopyWithinSameDirectory_CreatesUniqueCopy()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "same.txt");
        File.WriteAllText(source, "content");
        var conflicts = new ConflictResolverStub(FileConflictAction.Replace);
        var manager = new FileManagerStub();
        var coordinator = new FileOperationCoordinator(
            manager,
            new ProgressDialogStub(),
            conflicts);

        FileOperationBatchResult result = await coordinator.TransferAsync(
            [source],
            temp.Path,
            FileTransferKind.Copy);

        Assert.True(result.Success);
        Assert.Equal(
            "content", File.ReadAllText(Path.Combine(temp.Path, "same - Copy.txt")));
        Assert.Equal(0, conflicts.CallCount);
    }

    [Fact]
    public async Task TransferAsync_Replace_PublishesOnlyAfterCopyAndKeepsOriginalUntilThen()
    {
        using var temp = new TempDirectory();
        string sourceDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "source")).FullName;
        string destinationDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "destination")).FullName;
        string source = Path.Combine(sourceDirectory, "same.txt");
        string destination = Path.Combine(destinationDirectory, "same.txt");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");

        var manager = new FileManagerStub { BeforeCopy = (_, _) => Assert.Equal("old", File.ReadAllText(destination)) };
        var coordinator = new FileOperationCoordinator(
            manager,
            new ProgressDialogStub(),
            new ConflictResolverStub(FileConflictAction.Replace));

        FileOperationBatchResult result = await coordinator.TransferAsync(
            [source],
            destinationDirectory,
            FileTransferKind.Copy);

        Assert.True(result.Success);
        Assert.Empty(manager.DeletedPaths);
        Assert.Equal("new", File.ReadAllText(destination));
        Assert.Equal(["copy"], manager.OperationOrder);
        Assert.Empty(Directory.GetDirectories(destinationDirectory));
    }

    [Fact]
    public async Task DeleteAsync_CancellationRestoresEveryLocalItem()
    {
        using var temp = new TempDirectory();
        string first = Path.Combine(temp.Path, "first.bin");
        string second = Path.Combine(temp.Path, "second.bin");
        File.WriteAllBytes(first, new byte[10]);
        File.WriteAllBytes(second, new byte[20]);

        var progress = new ProgressDialogStub();
        progress.OnReport = value => { if(value == 0.5) progress.Cancel(); };
        var manager = new FileManagerStub();
        var coordinator = new FileOperationCoordinator(
            manager,
            progress,
            new ConflictResolverStub(FileConflictAction.Skip));

        int synchronizationCount = 0;
        FileOperationBatchResult result = await coordinator.DeleteAsync(
            [first, second],
            synchronizeViewAsync: () =>
            {
                synchronizationCount++;
                return Task.CompletedTask;
            });

        Assert.True(result.Canceled);
        Assert.False(result.HasPartialChanges);
        Assert.Equal(0, result.CompletedCount);
        Assert.Empty(manager.DeletedPaths);
        Assert.Equal(10, new FileInfo(first).Length);
        Assert.Equal(20, new FileInfo(second).Length);
        Assert.Empty(Directory.GetDirectories(temp.Path));
        Assert.Equal(1, synchronizationCount);
    }

    [Fact]
    public async Task DeleteAsync_KeepsWholeTreeAndRestoresAfterServiceRestart()
    {
        using var temp = new TempDirectory();
        string root = Directory.CreateDirectory(Path.Combine(temp.Path, "root")).FullName;
        string child = Directory.CreateDirectory(Path.Combine(root, "child")).FullName;
        string file = Path.Combine(child, "data.bin");
        File.WriteAllBytes(file, new byte[16]);
        var manager = new FileManagerStub();
        var coordinator = new FileOperationCoordinator(
            manager,
            new ProgressDialogStub(),
            new ConflictResolverStub(FileConflictAction.Skip));

        FileOperationBatchResult result = await coordinator.DeleteAsync([root]);

        Assert.True(result.Success);
        Assert.Empty(manager.DeletedPaths);
        Assert.False(Directory.Exists(root));
        var restarted = new FileOperationCoordinator(manager, new ProgressDialogStub(), new ConflictResolverStub(FileConflictAction.Skip));
        Assert.True((await restarted.RestoreAsync(temp.Path)).Success);
        Assert.Equal(16, new FileInfo(file).Length);
        Assert.Equal([root], Directory.GetDirectories(temp.Path));
    }

    [Fact]
    public void ValidateDestination_RejectsSelfAndDescendant()
    {
        using var temp = new TempDirectory();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "folder")).FullName;
        string descendant = Directory.CreateDirectory(Path.Combine(source, "child")).FullName;
        string sibling = Directory.CreateDirectory(Path.Combine(temp.Path, "sibling")).FullName;

        Assert.Throws<InvalidOperationException>(() =>
            FileOperationCoordinator.ValidateDestination(source, source, true));
        Assert.Throws<InvalidOperationException>(() =>
            FileOperationCoordinator.ValidateDestination(source, descendant, true));
        FileOperationCoordinator.ValidateDestination(source, sibling, true);
    }

    [Theory]
    [InlineData(FileTransferKind.Copy)]
    [InlineData(FileTransferKind.Move)]
    public async Task TransferAsync_CancelAfterFirstItemRollsBackWholeLocalBatch(FileTransferKind kind)
    {
        using var temp = new TempDirectory();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string target = Directory.CreateDirectory(Path.Combine(temp.Path, "target")).FullName;
        string a = Path.Combine(source, "a.txt"), b = Path.Combine(source, "b.txt");
        File.WriteAllText(a, "A"); File.WriteAllText(b, "B");
        File.WriteAllText(Path.Combine(target, "a.txt"), "old A");
        var progress = new ProgressDialogStub();
        progress.OnReport = value => { if(value == 0.5) progress.Cancel(); };
        var service = new FileOperationCoordinator(new FileManagerStub(), progress,
            new ConflictResolverStub(FileConflictAction.Replace));
        var result = await service.TransferAsync([a, b], target, kind);
        Assert.True(result.Canceled);
        Assert.False(result.HasPartialChanges);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal("A", File.ReadAllText(a)); Assert.Equal("B", File.ReadAllText(b));
        Assert.Equal("old A", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.False(File.Exists(Path.Combine(target, "b.txt")));
        Assert.Empty(Directory.GetDirectories(source)); Assert.Empty(Directory.GetDirectories(target));
    }

    [Fact]
    public async Task TransferAsync_FailureOnSecondItemRestoresPreviouslyReplacedTarget()
    {
        using var temp = new TempDirectory();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string target = Directory.CreateDirectory(Path.Combine(temp.Path, "target")).FullName;
        string a = Path.Combine(source, "a.txt"), b = Path.Combine(source, "b.txt");
        File.WriteAllText(a, "A"); File.WriteAllText(b, "B");
        File.WriteAllText(Path.Combine(target, "a.txt"), "old A");
        var manager = new FileManagerStub { BeforeCopy = (path, _) =>
        {
            if(path == b) throw new IOException("injected copy failure");
        } };
        var service = new FileOperationCoordinator(manager, new ProgressDialogStub(),
            new ConflictResolverStub(FileConflictAction.Replace));
        var result = await service.TransferAsync([a, b], target, FileTransferKind.Copy);
        Assert.False(result.Success);
        Assert.False(result.HasPartialChanges);
        Assert.Equal("old A", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Equal("A", File.ReadAllText(a)); Assert.Equal("B", File.ReadAllText(b));
        Assert.False(File.Exists(Path.Combine(target, "b.txt")));
        Assert.Empty(Directory.GetDirectories(target));
    }

    [Fact]
    public async Task TransferAsync_TargetChangedAfterConfirmationIsNotOverwritten()
    {
        using var temp = new TempDirectory();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string target = Directory.CreateDirectory(Path.Combine(temp.Path, "target")).FullName;
        string a = Path.Combine(source, "a.txt"), destination = Path.Combine(target, "a.txt");
        File.WriteAllText(a, "A"); File.WriteAllText(destination, "old A");
        var manager = new FileManagerStub { BeforeCopy = (_, _) =>
        {
            File.Move(destination, Path.Combine(target, "external-backup.txt"));
            File.WriteAllText(destination, "external");
        } };
        var service = new FileOperationCoordinator(manager, new ProgressDialogStub(),
            new ConflictResolverStub(FileConflictAction.Replace));
        Assert.False((await service.TransferAsync([a], target, FileTransferKind.Copy)).Success);
        Assert.Equal("external", File.ReadAllText(destination));
        Assert.Equal("A", File.ReadAllText(a));
    }

    private sealed class ConflictResolverStub: IFileConflictResolver
    {
        private readonly FileConflictAction _action;

        public ConflictResolverStub(FileConflictAction action)
        {
            _action = action;
        }

        public int CallCount { get; private set; }

        public Task<FileConflictDecision> ResolveAsync(
            string sourcePath,
            string destinationPath,
            bool isDirectory,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new FileConflictDecision(_action));
        }
    }

    private sealed class ProgressDialogStub: IProgressDialogService
    {
        private readonly CancellationTokenSource _cancellation = new();
        public List<double> Values { get; } = [];
        public bool IsOperationActive { get; private set; }
        public Action<double?>? OnReport { get; set; }

        public void Cancel() => _cancellation.Cancel();

        public async Task<T> RunAsync<T>(
            string operationName,
            Func<IProgressReporter, CancellationToken, Task<T>> operation)
        {
            IsOperationActive = true;
            try
            {
                return await operation(
                    new ProgressReporter(Values, value => OnReport?.Invoke(value)),
                    _cancellation.Token);
            }
            finally
            {
                IsOperationActive = false;
            }
        }

        private sealed class ProgressReporter: IProgressReporter
        {
            private readonly ICollection<double> _values;
            private readonly Action<double?> callback;

            public ProgressReporter(ICollection<double> values, Action<double?> callback)
            {
                _values = values;
                this.callback = callback;
            }

            public void Report(double? value, string? currentInfo = null)
            {
                if(value is not null)
                    _values.Add(value.Value);
                callback(value);
            }
        }
    }

    private sealed class FileManagerStub: IFileManagerService
    {
        public List<string> CopyDestinations { get; } = [];
        public List<string> DeletedPaths { get; } = [];
        public List<string> OperationOrder { get; } = [];
        public Action<string>? AfterDelete { get; init; }
        public Action<string, string>? BeforeCopy { get; init; }

        public Task<FileOperationResult> CopyAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OperationOrder.Add("copy");
            CopyDestinations.Add(destinationPath);
            BeforeCopy?.Invoke(sourcePath, destinationPath);
            File.Copy(sourcePath, destinationPath);
            progress?.Report(0.5, sourcePath);
            return Task.FromResult(FileOperationResult.Ok());
        }

        public Task<FileOperationResult> MoveAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            CopyAsync(sourcePath, destinationPath, progress, cancellationToken);

        public Task<FileOperationResult> DeleteAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OperationOrder.Add("delete");
            DeletedPaths.Add(path);
            AfterDelete?.Invoke(path);
            return Task.FromResult(FileOperationResult.Ok());
        }

        public Task<List<ISystemItem>> BrowseAsync(
            string path,
            IProgressReporter? progress = null,
            CancellationToken ct = default,
            bool includeHidden = false) => throw new NotSupportedException();

        public Task<FileOperationResult> RenameAsync(
            string path,
            string newName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FileOperationResult> CreateDirectoryAsync(
            string parentDirectory,
            string newDirectoryName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> CanReadAsync(
            string path,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> CanWriteAsync(
            string path,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            string path,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Stream> OpenWriteAsync(
            string path,
            bool overwrite,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsHiddenAsync(
            string path,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FileOperationResult> SetHiddenAsync(
            string path,
            bool hidden,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsReadOnlyAsync(
            string path,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FileOperationResult> SetReadOnlyAsync(
            string path,
            bool isReadOnly,
            CancellationToken ct = default) => throw new NotSupportedException();

        public string NormalizePath(string rawPath) => rawPath;
    }

    private sealed class TempDirectory: IDisposable
    {
        public TempDirectory()
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

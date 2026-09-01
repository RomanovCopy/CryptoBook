using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Collections.Specialized;
using System.IO;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileExplorerCollectionPerformanceTests
{
    [Fact]
    public async Task AddChildAsync_TenThousandItems_UsesLinearLookupAndOneReset()
    {
        DirectoryItem container = CreateContainer();
        ISystemItem[] items = CreateItems(0, 10_000);
        int keyCalls = 0;
        int resetEvents = 0;
        ((INotifyCollectionChanged)container.Children).CollectionChanged += (_, args) =>
        {
            if(args.Action == NotifyCollectionChangedAction.Reset)
                resetEvents++;
        };

        FileOperationResult result = await container.AddChildAsync(
            items,
            item =>
            {
                keyCalls++;
                return item.FullPath;
            });

        Assert.True(result.Success);
        Assert.Equal(10_000, container.Children.Count);
        Assert.InRange(keyCalls, 10_000, 20_000);
        Assert.Equal(1, resetEvents);
    }

    [Fact]
    public async Task SyncCollectionsAsync_TenThousandItems_IsLinearAndBatched()
    {
        DirectoryItem container = CreateContainer();
        await container.AddChildAsync(
            CreateItems(0, 10_000),
            item => item.FullPath);
        ISystemItem[] incoming = CreateItems(5_000, 10_000);
        int keyCalls = 0;
        int resetEvents = 0;
        ((INotifyCollectionChanged)container.Children).CollectionChanged += (_, args) =>
        {
            if(args.Action == NotifyCollectionChangedAction.Reset)
                resetEvents++;
        };

        await container.SyncCollectionsAsync(
            incoming,
            item =>
            {
                keyCalls++;
                return item.FullPath;
            },
            updateExisting: null,
            CancellationToken.None);

        Assert.Equal(10_000, container.Children.Count);
        Assert.Equal("file-005000.txt", container.Children[0].Name);
        Assert.Equal("file-014999.txt", container.Children[^1].Name);
        Assert.InRange(keyCalls, 1, 160_000);
        Assert.Equal(1, resetEvents);
    }

    [Fact]
    public async Task SortingAsync_UsesSingleCollectionReset()
    {
        DirectoryItem container = CreateContainer();
        await container.AddChildAsync(
            CreateItems(0, 1_000).Reverse(),
            item => item.FullPath);
        int resetEvents = 0;
        ((INotifyCollectionChanged)container.Children).CollectionChanged += (_, args) =>
        {
            if(args.Action == NotifyCollectionChangedAction.Reset)
                resetEvents++;
        };

        await container.SortingAsync(SystemItemSortType.Name);

        Assert.Equal("file-000000.txt", container.Children[0].Name);
        Assert.Equal("file-000999.txt", container.Children[^1].Name);
        Assert.Equal(1, resetEvents);
    }

    [Fact]
    public async Task SingleWatcherAdd_UsesIncrementalNotification()
    {
        DirectoryItem container = CreateContainer();
        await container.AddChildAsync(
            CreateItems(0, 3),
            item => item.FullPath);
        var actions = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)container.Children).CollectionChanged +=
            (_, args) => actions.Add(args.Action);

        ISystemItem added = CreateItems(3, 1)[0];
        FileOperationResult result = await container.AddChildAsync(
            [added],
            item => item.FullPath);

        Assert.True(result.Success);
        Assert.Equal([NotifyCollectionChangedAction.Add], actions);
        Assert.Same(added, container.Children[^1]);
    }

    [Fact]
    public async Task SingleWatcherDelete_PreservesRemainingItemsWithoutReset()
    {
        DirectoryItem container = CreateContainer();
        ISystemItem[] original = CreateItems(0, 3);
        await container.AddChildAsync(original, item => item.FullPath);
        var actions = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)container.Children).CollectionChanged +=
            (_, args) => actions.Add(args.Action);

        FileOperationResult result = await container.RemoveChildAsync(
            [original[1]],
            item => item.FullPath);

        Assert.True(result.Success);
        Assert.Equal([NotifyCollectionChangedAction.Remove], actions);
        Assert.Equal<ISystemItem>(
            [original[0], original[2]],
            container.Children);
    }

    [Fact]
    public async Task WatcherRename_UpdatesExistingItemWithoutCollectionReset()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string oldPath = Path.Combine(directory, "before.txt");
            string newPath = Path.Combine(directory, "after.txt");
            await File.WriteAllTextAsync(oldPath, "content");
            var monitoring = new MonitoringStub();
            DirectoryItem container = CreateContainer(
                monitoring,
                new LocalItemFactoryStub());
            container.FullPath = directory;
            var original = new FileItem
            {
                Name = Path.GetFileName(oldPath),
                FullPath = oldPath,
                Extension = ".txt",
                Parent = container
            };
            await container.AddChildAsync([original], item => item.FullPath);
            var actions = new List<NotifyCollectionChangedAction>();
            ((INotifyCollectionChanged)container.Children).CollectionChanged +=
                (_, args) => actions.Add(args.Action);
            container.IsSelected = true;

            File.Move(oldPath, newPath);
            monitoring.Renamed?.Invoke(new RenamedEventArgs(
                WatcherChangeTypes.Renamed,
                directory,
                Path.GetFileName(newPath),
                Path.GetFileName(oldPath)));

            await WaitUntilAsync(() =>
                string.Equals(
                    original.FullPath,
                    newPath,
                    StringComparison.OrdinalIgnoreCase));

            Assert.Empty(actions);
            Assert.Same(original, Assert.Single(container.Children));
            Assert.Equal("after.txt", original.Name);
        }
        finally
        {
            if(Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WatcherChanged_DispatcherCancellation_IsHandled()
    {
        var monitoring = new MonitoringStub();
        var dispatcher = new CancelingDispatcher();
        DirectoryItem container = CreateContainer(
            monitoring,
            dispatcher: dispatcher);
        container.FullPath = "C:\\catalog";
        container.IsSelected = true;

        monitoring.Changed?.Invoke(new FileSystemEventArgs(
            WatcherChangeTypes.Changed,
            container.FullPath,
            "file.txt"));
        await Task.Delay(50);

        Assert.Equal(1, dispatcher.InvocationCount);
    }

    [Fact]
    public async Task DirectoryChildren_ExcludesFiles_AndIgnoresFileOnlyUpdates()
    {
        DirectoryItem container = CreateContainer();
        DirectoryItem firstDirectory = CreateContainer();
        firstDirectory.Name = "folder-a";
        firstDirectory.FullPath = "C:\\catalog\\folder-a";

        int treeResetEvents = 0;
        ((INotifyCollectionChanged)container.DirectoryChildren).CollectionChanged += (_, args) =>
        {
            if(args.Action == NotifyCollectionChangedAction.Reset)
                treeResetEvents++;
        };

        await container.AddChildAsync(
            CreateItems(0, 10_000).Append(firstDirectory),
            item => item.FullPath);

        Assert.Single(container.DirectoryChildren);
        Assert.Same(firstDirectory, container.DirectoryChildren[0]);
        Assert.Equal(1, treeResetEvents);

        await container.AddChildAsync(
            CreateItems(10_000, 1),
            item => item.FullPath);

        Assert.Single(container.DirectoryChildren);
        Assert.Equal(1, treeResetEvents);
    }

    private static DirectoryItem CreateContainer(
        MonitoringStub? monitoring = null,
        ISystemItemCreateService? itemFactory = null,
        IDispatcherService? dispatcher = null) => new(
            dispatcher ?? new ImmediateDispatcher(),
            monitoring ?? new MonitoringStub(),
            itemFactory ?? new ItemFactoryStub(),
            new SystemItemSortService());

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while(!condition())
            await Task.Delay(20, timeout.Token);
    }

    private static ISystemItem[] CreateItems(int start, int count) =>
        Enumerable.Range(start, count)
            .Select(index =>
            {
                string name = $"file-{index:D6}.txt";
                return (ISystemItem)new FileItem
                {
                    Name = name,
                    FullPath = Path.Combine("C:\\catalog", name),
                    Extension = ".txt"
                };
            })
            .ToArray();

    private sealed class ImmediateDispatcher: IDispatcherService
    {
        public bool CheckAccess() => true;
        public void Invoke(Action action) => action();
        public void BeginInvoke(Action action) => action();
        public Task InvokeAsync(
            Action action,
            DispatcherPriority priority = DispatcherPriority.Background)
        {
            action();
            return Task.CompletedTask;
        }
        public Task<T> InvokeAsync<T>(
            Func<T> func,
            DispatcherPriority priority = DispatcherPriority.Background) =>
            Task.FromResult(func());
    }

    private sealed class CancelingDispatcher: IDispatcherService
    {
        private int invocationCount;

        public int InvocationCount => Volatile.Read(ref invocationCount);
        public bool CheckAccess() => false;
        public void Invoke(Action action) => throw new NotSupportedException();
        public void BeginInvoke(Action action) => throw new NotSupportedException();
        public Task InvokeAsync(
            Action action,
            DispatcherPriority priority = DispatcherPriority.Background)
        {
            Interlocked.Increment(ref invocationCount);
            return Task.FromCanceled(new CancellationToken(canceled: true));
        }
        public Task<T> InvokeAsync<T>(
            Func<T> func,
            DispatcherPriority priority = DispatcherPriority.Background)
        {
            Interlocked.Increment(ref invocationCount);
            return Task.FromCanceled<T>(new CancellationToken(canceled: true));
        }
    }

    private sealed class MonitoringStub: IDirectoryMonitoringService
    {
        public Action<RenamedEventArgs>? Renamed { get; private set; }
        public Action<FileSystemEventArgs>? Changed { get; private set; }

        public bool StartMonitoring(
            string directoryPath,
            Action<FileSystemEventArgs>? onCreated = null,
            Action<FileSystemEventArgs>? onDeleted = null,
            Action<RenamedEventArgs>? onRenamed = null,
            Action<FileSystemEventArgs>? onChanged = null,
            Action<Exception?>? onOverflowOrError = null,
            bool includeSubdirectories = false,
            NotifyFilters notifyFilters = NotifyFilters.FileName |
                NotifyFilters.DirectoryName |
                NotifyFilters.LastWrite,
            int internalBufferSize = 64 * 1024)
        {
            Renamed = onRenamed;
            Changed = onChanged;
            return true;
        }
        public bool StopMonitoring(string directoryPath) => true;
        public void Dispose()
        {
        }
    }

    private sealed class ItemFactoryStub: ISystemItemCreateService
    {
        public IDriveItem CreateRoot(string rootPath) =>
            throw new NotSupportedException();
        public IDirectoryItem CreateDirectory(
            string path,
            ISystemItem? parent) => throw new NotSupportedException();
        public IFileItem CreateFile(
            string path,
            ISystemItem? parent) => throw new NotSupportedException();
    }

    private sealed class LocalItemFactoryStub: ISystemItemCreateService
    {
        public IDriveItem CreateRoot(string rootPath) =>
            throw new NotSupportedException();

        public IDirectoryItem CreateDirectory(
            string path,
            ISystemItem? parent) =>
            throw new NotSupportedException();

        public IFileItem CreateFile(string path, ISystemItem? parent)
        {
            var info = new FileInfo(path);
            return new FileItem
            {
                Name = info.Name,
                FullPath = info.FullName,
                RootDirectory = info.DirectoryName ?? string.Empty,
                Extension = info.Extension,
                Size = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                Parent = parent
            };
        }
    }
}

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

    private static DirectoryItem CreateContainer() => new(
        new ImmediateDispatcher(),
        new MonitoringStub(),
        new ItemFactoryStub(),
        new SystemItemSortService());

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

    private sealed class MonitoringStub: IDirectoryMonitoringService
    {
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
            int internalBufferSize = 64 * 1024) => true;
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
}

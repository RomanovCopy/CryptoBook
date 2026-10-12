using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.Reflection;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DriveRefreshTests
{
    [Theory]
    [InlineData("E:")]
    [InlineData("E:\\")]
    [InlineData("e:\\")]
    public async Task VolumeRemoval_RemovesDriveFromCacheAndExplorerCollection(string eventPath)
    {
        var factory = new RootFactory();
        IDriveItem systemDrive = factory.CreateRoot(@"C:\");
        IDriveItem removableDrive = factory.CreateRoot(@"E:\");
        using var monitoring = new DriveMonitoringService(
            factory,
            () => [systemDrive, removableDrive]);
        using var manager = new DriveManagerService(monitoring, new ImmediateDispatcher());
        IReadOnlyList<IDriveItem> originalSnapshot = monitoring.GetWritableDrives();
        var disconnected = new List<string>();
        manager.DriveDisconnected += disconnected.Add;

        await monitoring.HandleVolumeChangeAsync(3, eventPath);

        Assert.Same(systemDrive, Assert.Single(monitoring.GetWritableDrives()));
        Assert.Same(systemDrive, Assert.Single(manager.WritableDrives));
        Assert.Equal(@"E:\", Assert.Single(disconnected), ignoreCase: true);
        Assert.Equal(2, originalSnapshot.Count);
    }

    [Fact]
    public async Task Refresh_RecoversMissedChangesAndPreservesConnectedRootInstances()
    {
        var factory = new RootFactory();
        IDriveItem systemDrive = factory.CreateRoot(@"C:\");
        IDriveItem removedDrive = factory.CreateRoot(@"E:\");
        IDriveItem addedDrive = factory.CreateRoot(@"F:\");
        IReadOnlyList<IDriveItem> detected = [systemDrive, removedDrive];
        using var monitoring = new DriveMonitoringService(factory, () => detected);
        using var manager = new DriveManagerService(monitoring, new ImmediateDispatcher());
        var disconnected = new List<string>();
        var connected = new List<IDriveItem>();
        manager.DriveDisconnected += disconnected.Add;
        manager.DriveConnected += connected.Add;

        // The explorer was closed and no device-change event was delivered.
        detected = [factory.CreateRoot(@"C:\"), addedDrive];
        await manager.RefreshAsync();
        await manager.RefreshAsync();

        Assert.Equal(2, manager.WritableDrives.Count);
        Assert.Same(systemDrive, manager.WritableDrives[0]);
        Assert.Same(addedDrive, manager.WritableDrives[1]);
        Assert.Equal([@"E:\"], disconnected);
        Assert.Same(addedDrive, Assert.Single(connected));
    }

    [Fact]
    public async Task Refresh_RemovesDisconnectedPortableDeviceWithoutRemovingLocalDrive()
    {
        var factory = new RootFactory();
        IDriveItem systemDrive = factory.CreateRoot(@"C:\");
        using var monitoring = new DriveMonitoringService(factory, () => [systemDrive]);
        IStorageFacade storage = DispatchProxy.Create<IStorageFacade, RootsProxy>();
        var roots = (RootsProxy)storage;
        StorageItemMetadata phone = CreatePhone();
        roots.ReadRoots = _ => Task.FromResult<IReadOnlyList<StorageItemMetadata>>([phone]);
        using var manager = new DriveManagerService(
            monitoring, new ImmediateDispatcher(), storage, factory);
        await manager.RefreshAsync();
        IDriveItem phoneRoot = Assert.Single(manager.WritableDrives, drive => !drive.Location.IsLocal);
        var disconnected = new List<string>();
        manager.DriveDisconnected += disconnected.Add;

        roots.ReadRoots = _ => Task.FromResult<IReadOnlyList<StorageItemMetadata>>([]);
        await manager.RefreshAsync();

        Assert.Same(systemDrive, Assert.Single(manager.WritableDrives));
        Assert.Equal([phoneRoot.FullPath], disconnected);
    }

    [Fact]
    public async Task Refresh_UnavailablePortableTransportStillRefreshesLocalDrives()
    {
        var factory = new RootFactory();
        IDriveItem systemDrive = factory.CreateRoot(@"C:\");
        IDriveItem removedDrive = factory.CreateRoot(@"E:\");
        IReadOnlyList<IDriveItem> detected = [systemDrive, removedDrive];
        using var monitoring = new DriveMonitoringService(factory, () => detected);
        IStorageFacade storage = DispatchProxy.Create<IStorageFacade, RootsProxy>();
        ((RootsProxy)storage).ReadRoots = _ =>
            Task.FromException<IReadOnlyList<StorageItemMetadata>>(new System.IO.IOException("Offline"));
        using var manager = new DriveManagerService(
            monitoring, new ImmediateDispatcher(), storage, factory);

        detected = [systemDrive];
        await manager.RefreshAsync();

        Assert.Same(systemDrive, Assert.Single(manager.WritableDrives));
    }

    [Fact]
    public async Task Refresh_SerializesDeviceDiscoverySoOlderResultsCannotRestoreRemovedDevice()
    {
        var factory = new RootFactory();
        using var monitoring = new DriveMonitoringService(factory, () => []);
        IStorageFacade storage = DispatchProxy.Create<IStorageFacade, RootsProxy>();
        var roots = (RootsProxy)storage;
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResult = new TaskCompletionSource<IReadOnlyList<StorageItemMetadata>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int discoveryCount = 0;
        roots.ReadRoots = _ =>
        {
            if(Interlocked.Increment(ref discoveryCount) == 1)
            {
                firstStarted.SetResult();
                return firstResult.Task;
            }
            return Task.FromResult<IReadOnlyList<StorageItemMetadata>>([]);
        };
        using var manager = new DriveManagerService(
            monitoring, new ImmediateDispatcher(), storage, factory);

        Task firstRefresh = manager.RefreshAsync();
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task secondRefresh = manager.RefreshAsync();
        Assert.Equal(1, Volatile.Read(ref discoveryCount));
        firstResult.SetResult([CreatePhone()]);
        await Task.WhenAll(firstRefresh, secondRefresh).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, discoveryCount);
        Assert.Empty(manager.WritableDrives);
    }

    [WpfFact]
    public async Task ExplorerLoaded_WaitsForFreshDrivesBeforeRestoringLastDirectory()
    {
        IFileExplorerModel model = DispatchProxy.Create<IFileExplorerModel, ExplorerModelProxy>();
        var state = (ExplorerModelProxy)model;
        IFavoriteDirectoriesViewModel favorites =
            DispatchProxy.Create<IFavoriteDirectoriesViewModel, PassiveProxy>();
        var viewModel = new FileExplorerViewModel(
            model,
            favorites,
            DispatchProxy.Create<IFilePreviewViewModel, PassiveProxy>(),
            DispatchProxy.Create<IFileExplorerFlatViewService, PassiveProxy>(),
            DispatchProxy.Create<IMessageService, PassiveProxy>(),
            DispatchProxy.Create<IFilePropertiesService, PassiveProxy>(),
            DispatchProxy.Create<IWindowManager, PassiveProxy>(),
            new WindowContext(new Dictionary<string, object?>()));
        try
        {
            viewModel.Loaded.Execute(null);
            Assert.True(state.RefreshRequested);
            Assert.False(state.DirectoryRestored.Task.IsCompleted);

            state.DrivesRefreshed.SetResult();
            await state.DirectoryRestored.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            viewModel.Closed.Execute(null);
        }
    }

    private static StorageItemMetadata CreatePhone() => new(
        WpdLocatorCodec.Encode("phone-id", "/"),
        "Phone",
        StorageItemKind.Root,
        Capabilities: StorageProviderCapabilities.Browse,
        StatusText: "online");

    public class RootsProxy: DispatchProxy
    {
        public Func<CancellationToken, Task<IReadOnlyList<StorageItemMetadata>>> ReadRoots { get; set; } =
            _ => Task.FromResult<IReadOnlyList<StorageItemMetadata>>([]);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IStorageFacade.GetRootsAsync)
                ? ReadRoots((CancellationToken)args![0]!)
                : throw new NotSupportedException(targetMethod?.Name);
    }

    public class ExplorerModelProxy: PassiveProxy
    {
        public bool RefreshRequested { get; private set; }
        public TaskCompletionSource DrivesRefreshed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DirectoryRestored { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.Name == nameof(IFileExplorerModel.RefreshDrivesAsync))
            {
                RefreshRequested = true;
                return DrivesRefreshed.Task;
            }
            if(targetMethod?.Name == nameof(IFileExplorerModel.RestoreLastDirectoryAsync))
            {
                DirectoryRestored.SetResult();
                return Task.CompletedTask;
            }
            return base.Invoke(targetMethod, args);
        }
    }

    public class PassiveProxy: DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Type returnType = targetMethod!.ReturnType;
            if(returnType == typeof(Task))
                return Task.CompletedTask;
            return returnType.IsValueType && returnType != typeof(void)
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private sealed class RootFactory: ISystemItemCreateService
    {
        public IDriveItem CreateRoot(string rootPath) => new DriveItem(null!, null!, null!, null!)
        {
            FullPath = rootPath,
            RootDirectory = rootPath,
            Name = rootPath
        };

        public IDriveItem CreateRoot(StorageItemMetadata metadata)
        {
            IDriveItem root = CreateRoot(metadata.Location.ToString());
            root.Name = metadata.Name;
            root.Capabilities = metadata.Capabilities;
            root.StatusText = metadata.StatusText;
            return root;
        }

        public IDirectoryItem CreateDirectory(string path, ISystemItem? parent) =>
            throw new NotSupportedException();
        public IFileItem CreateFile(string path, ISystemItem? parent) =>
            throw new NotSupportedException();
    }

    private sealed class ImmediateDispatcher: IDispatcherService
    {
        public bool CheckAccess() => true;
        public void Invoke(Action action) => action();
        public void BeginInvoke(Action action) => action();
        public Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            action();
            return Task.CompletedTask;
        }
        public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Background) =>
            Task.FromResult(func());
    }
}

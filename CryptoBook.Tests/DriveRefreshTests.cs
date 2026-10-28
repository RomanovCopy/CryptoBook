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

    [Fact]
    public async Task VolumeRemoval_DiscardsPendingConnectionEvenIfReadinessIgnoresCancellation()
    {
        var factory = new RootFactory();
        var ready = new TaskCompletionSource<IDriveItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitoring = new DriveMonitoringService(factory, () => [],
            waitForDriveReady: (_, _) => ready.Task);
        using var manager = new DriveManagerService(monitoring, new ImmediateDispatcher());
        var connected = new List<IDriveItem>();
        manager.DriveConnected += connected.Add;

        Task connection = monitoring.HandleVolumeChangeAsync(2, "E:");
        await monitoring.HandleVolumeChangeAsync(3, "e:\\");
        ready.SetResult(factory.CreateRoot(@"E:\"));
        await connection;

        Assert.Empty(monitoring.GetWritableDrives());
        Assert.Empty(manager.WritableDrives);
        Assert.Empty(connected);
    }

    [Fact]
    public async Task Reconnection_KeepsNewDeviceWhenOlderReadinessCompletesLast()
    {
        var factory = new RootFactory();
        var oldReady = new TaskCompletionSource<IDriveItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        IDriveItem newDrive = factory.CreateRoot(@"E:\");
        int attempts = 0;
        using var monitoring = new DriveMonitoringService(factory, () => [],
            waitForDriveReady: (_, _) => ++attempts == 1 ? oldReady.Task : Task.FromResult<IDriveItem?>(newDrive));

        Task connection = monitoring.HandleVolumeChangeAsync(2, "E:");
        await monitoring.HandleVolumeChangeAsync(3, "E:");
        await monitoring.HandleVolumeChangeAsync(2, "E:");
        oldReady.SetResult(factory.CreateRoot(@"E:\"));
        await connection;

        Assert.Same(newDrive, Assert.Single(monitoring.GetWritableDrives()));
    }

    [Fact]
    public async Task Refresh_DoesNotDiscardArrivalStillWaitingForDeviceReadiness()
    {
        var factory = new RootFactory();
        var ready = new TaskCompletionSource<IDriveItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitoring = new DriveMonitoringService(factory, () => [],
            waitForDriveReady: (_, _) => ready.Task);
        Task connection = monitoring.HandleVolumeChangeAsync(2, "E:");

        monitoring.RefreshCurrentDrives();
        ready.SetResult(factory.CreateRoot(@"E:\"));
        await connection;

        Assert.Equal(@"E:\", Assert.Single(monitoring.GetWritableDrives()).RootDirectory);
    }

    [Fact]
    public async Task Monitoring_StopCancelsPendingConnectionAndRestartCreatesFreshSubscription()
    {
        var factory = new RootFactory();
        var subscriptions = new List<Subscription>();
        var pending = new TaskCompletionSource<IDriveItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken pendingToken = default;
        int attempts = 0;
        using var monitoring = new DriveMonitoringService(factory, () => [],
            _ => { var subscription = new Subscription(); subscriptions.Add(subscription); return subscription; },
            (_, token) =>
            {
                pendingToken = token;
                return ++attempts == 1 ? pending.Task : Task.FromResult<IDriveItem?>(factory.CreateRoot(@"E:\"));
            });

        monitoring.StartMonitoring();
        monitoring.StartMonitoring();
        Task connection = monitoring.HandleVolumeChangeAsync(2, "E:");
        monitoring.StopMonitoring();
        monitoring.StopMonitoring();
        Assert.True(pendingToken.IsCancellationRequested);
        Assert.Equal(1, Assert.Single(subscriptions).DisposeCount);
        monitoring.StartMonitoring();
        await monitoring.HandleVolumeChangeAsync(2, "E:");
        pending.SetResult(factory.CreateRoot(@"F:\"));
        await connection;

        Assert.False(pendingToken.IsCancellationRequested);
        Assert.Equal(2, subscriptions.Count);
        Assert.Equal(@"E:\", Assert.Single(monitoring.GetWritableDrives()).RootDirectory);
    }

    [Fact]
    public async Task Monitoring_DisposeCancelsPendingWorkAndIgnoresLateEvents()
    {
        var factory = new RootFactory();
        var pending = new TaskCompletionSource<IDriveItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var monitoring = new DriveMonitoringService(factory, () => [],
            waitForDriveReady: (_, current) => { token = current; return pending.Task; });
        Task connection = monitoring.HandleVolumeChangeAsync(2, "E:");
        monitoring.Dispose();
        monitoring.Dispose();
        pending.SetResult(factory.CreateRoot(@"E:\"));
        await connection;
        await monitoring.HandleVolumeChangeAsync(3, "E:");

        Assert.True(token.IsCancellationRequested);
        Assert.Empty(monitoring.GetWritableDrives());
        Assert.Throws<ObjectDisposedException>(monitoring.StartMonitoring);
    }

    [Fact]
    public async Task Manager_RestartsPortableDiscoveryAndDropsPreviousSessionResult()
    {
        var factory = new RootFactory();
        var subscriptions = new List<Subscription>();
        using var monitoring = new DriveMonitoringService(factory, () => [],
            _ => { var subscription = new Subscription(); subscriptions.Add(subscription); return subscription; });
        IStorageFacade storage = DispatchProxy.Create<IStorageFacade, RootsProxy>();
        var first = new TaskCompletionSource<IReadOnlyList<StorageItemMetadata>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var restarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        ((RootsProxy)storage).ReadRoots = _ =>
        {
            if(Interlocked.Increment(ref calls) == 1)
                return first.Task;
            restarted.TrySetResult();
            return Task.FromResult<IReadOnlyList<StorageItemMetadata>>([]);
        };
        using var manager = new DriveManagerService(monitoring, new ImmediateDispatcher(), storage, factory);

        manager.StartMonitoring();
        manager.StartMonitoring();
        Assert.Equal(1, calls);
        manager.StopMonitoring();
        manager.StartMonitoring();
        first.SetResult([CreatePhone()]);
        await restarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await manager.RefreshAsync();

        Assert.Empty(manager.WritableDrives);
        Assert.Equal(2, subscriptions.Count);
        Assert.Equal(1, subscriptions[0].DisposeCount);
    }

    [Fact]
    public async Task QueuedVolumeNotifications_ReconcileCurrentSnapshotWithoutRestoringRemovedDrive()
    {
        var factory = new RootFactory();
        using var monitoring = new DriveMonitoringService(factory, () => [],
            waitForDriveReady: (root, _) => Task.FromResult<IDriveItem?>(factory.CreateRoot(root)));
        var dispatcher = new QueuedDispatcher();
        using var manager = new DriveManagerService(monitoring, dispatcher);

        await monitoring.HandleVolumeChangeAsync(2, "E:");
        await monitoring.HandleVolumeChangeAsync(3, "E:");
        dispatcher.Flush();

        Assert.Empty(manager.WritableDrives);
    }

    [Fact]
    public async Task WindowsDeviceChange_RefreshesPortableRootsWithoutWaitingForPollingInterval()
    {
        var factory = new RootFactory();
        using var monitoring = new DriveMonitoringService(factory, () => [], _ => new Subscription());
        IStorageFacade storage = DispatchProxy.Create<IStorageFacade, RootsProxy>();
        IReadOnlyList<StorageItemMetadata> roots = [CreatePhone()];
        ((RootsProxy)storage).ReadRoots = _ => Task.FromResult(roots);
        using var manager = new DriveManagerService(monitoring, new ImmediateDispatcher(), storage, factory);
        var removed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.DriveDisconnected += _ => removed.TrySetResult();
        manager.StartMonitoring();
        Assert.Single(manager.WritableDrives);

        roots = [];
        monitoring.HandleDeviceChange(3);
        await removed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(manager.WritableDrives);
    }

    [Theory]
    [InlineData("offline")]
    [InlineData("disconnected")]
    public async Task Refresh_DoesNotKeepOfflinePortableRootAmongConnectedDevices(string status)
    {
        var factory = new RootFactory();
        using var monitoring = new DriveMonitoringService(factory, () => []);
        IStorageFacade storage = DispatchProxy.Create<IStorageFacade, RootsProxy>();
        StorageItemMetadata phone = CreatePhone();
        ((RootsProxy)storage).ReadRoots = _ => Task.FromResult<IReadOnlyList<StorageItemMetadata>>([phone]);
        using var manager = new DriveManagerService(monitoring, new ImmediateDispatcher(), storage, factory);
        await manager.RefreshAsync();
        Assert.Single(manager.WritableDrives);

        phone = phone with { Capabilities = StorageProviderCapabilities.None, StatusText = status };
        await manager.RefreshAsync();

        Assert.Empty(manager.WritableDrives);
    }

    private sealed class Subscription: IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class QueuedDispatcher: IDispatcherService
    {
        private readonly Queue<Action> actions = new();
        public bool CheckAccess() => false;
        public void Invoke(Action action) => throw new InvalidOperationException("Device callbacks must not synchronously wait for the UI.");
        public void BeginInvoke(Action action) => actions.Enqueue(action);
        public Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Background) => throw new NotSupportedException();
        public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Background) => throw new NotSupportedException();
        public void Flush() { while(actions.TryDequeue(out Action? action)) action(); }
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

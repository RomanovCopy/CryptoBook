using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.Reflection;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileExplorerDeviceAvailabilityTests
{
    [WpfTheory]
    [InlineData("/")]
    [InlineData("/Download")]
    public async Task Disconnect_BlocksCurrentFolderAndRetryRecoversAfterReconnect(string path)
    {
        using var explorer = new ExplorerFixture();
        await explorer.OpenAsync(path);
        Assert.NotEmpty(explorer.ViewModel.ChildrenView.Cast<ISystemItem>());
        var file = new FileItem { FullPath = WpdLocatorCodec.Encode("phone", "/Download/note.txt").ToString(), Capabilities = StorageProviderCapabilities.Read };
        explorer.Model.SelectedItemsSnapshot = [file];
        Assert.True(explorer.Model.CanExecute_CopyCommand(new[] { file }));

        explorer.Devices.Clear();
        await explorer.Manager.RefreshAsync();

        Assert.True(explorer.Model.IsCurrentDirectoryUnavailable);
        Assert.Empty(explorer.Manager.WritableDrives);
        Assert.Empty(explorer.ViewModel.ChildrenView.Cast<ISystemItem>());
        Assert.True(explorer.Model.CanExecute_RetryNavigationCommand(null));
        Assert.False(explorer.Model.CanExecute_CopyCommand(new[] { file }));
        Assert.Empty(explorer.Model.SelectedItemsSnapshot);

        explorer.Devices.Add(new("phone", "Phone"));
        await explorer.Manager.RefreshAsync();
        Assert.True(explorer.Model.IsCurrentDirectoryUnavailable);
        await explorer.OpenAsync(path);
        Assert.False(explorer.Model.IsCurrentDirectoryUnavailable);
        Assert.False(explorer.Model.CanExecute_RetryNavigationCommand(null));
    }

    [WpfTheory]
    [InlineData("offline", FileExplorerNavigationErrorKind.DriveNotReady)]
    [InlineData("unauthorized", FileExplorerNavigationErrorKind.AccessDenied)]
    public async Task AccessLost_UpdatesOpenNestedFolderEvenWhenDeviceRootRemains(
        string status, FileExplorerNavigationErrorKind expectedError)
    {
        using var explorer = new ExplorerFixture();
        await explorer.OpenAsync("/Download");
        IDriveItem root = Assert.Single(explorer.Manager.WritableDrives);

        explorer.Devices[0] = new("phone", "Phone", false, status);
        await explorer.Manager.RefreshAsync();

        if(status == "offline")
            Assert.Empty(explorer.Manager.WritableDrives);
        else
            Assert.Same(root, Assert.Single(explorer.Manager.WritableDrives));
        Assert.True(explorer.Model.IsCurrentDirectoryUnavailable);
        Assert.Empty(explorer.ViewModel.ChildrenView.Cast<ISystemItem>());
        Assert.Equal(expectedError, explorer.Model.LastNavigationError);
        Assert.True(explorer.Model.CanExecute_RetryNavigationCommand(null));
    }

    [WpfFact]
    public async Task ClosedExplorer_UnsubscribesFromRootAndDriveCollectionChanges()
    {
        using var explorer = new ExplorerFixture();
        await explorer.OpenAsync("/Download");
        IDriveItem root = Assert.Single(explorer.Manager.WritableDrives);
        explorer.Close();

        root.Capabilities = StorageProviderCapabilities.None;
        explorer.Devices.Clear();
        await explorer.Manager.RefreshAsync();

        Assert.False(explorer.Model.IsCurrentDirectoryUnavailable);
    }

    private sealed class ExplorerFixture: IDisposable
    {
        private readonly DriveMonitoringService monitoring;
        private readonly string previousDirectory = CryptoBook.Properties.Settings.Default.LastDirectory_FileExplorer;
        private bool closed;
        public List<WpdDeviceInfo> Devices { get; } = [new("phone", "Phone")];
        public DriveManagerService Manager { get; }
        public FileExplorerModel Model { get; }
        public FileExplorerViewModel ViewModel { get; }

        public ExplorerFixture()
        {
            var dispatcher = new WpfDispatcherService(Dispatcher.CurrentDispatcher);
            var factory = new ItemFactory(dispatcher);
            var bridge = new WpdStorageProviderTests.BridgeStub { Devices = Devices };
            bridge.Entries[("phone", "/Download")] = new("/Download", "Download", true, 0, null);
            bridge.Entries[("phone", "/Download/note.txt")] = new("/Download/note.txt", "note.txt", false, 3, null);
            var provider = new WpdStorageProvider(bridge);
            var storage = new StorageFacade([new LocalStorageProvider(), provider]);
            monitoring = new DriveMonitoringService(factory, () => []);
            Manager = new DriveManagerService(monitoring, dispatcher, storage, factory);
            var files = new FileManagerService([new WpdFileProviderAdapter(provider, factory)], storage);
            ConstructorInfo constructor = typeof(FileExplorerModel).GetConstructors().Single();
            object?[] services = constructor.GetParameters().Select(parameter =>
                parameter.ParameterType == typeof(IFileManagerService) ? (object)files :
                parameter.ParameterType == typeof(IDriveManagerService) ? Manager :
                parameter.ParameterType == typeof(ISystemItemCreateService) ? factory :
                parameter.ParameterType == typeof(IStorageFacade) ? storage :
                parameter.HasDefaultValue ? parameter.DefaultValue :
                DispatchProxy.Create(parameter.ParameterType, typeof(PassiveProxy))).ToArray();
            Model = (FileExplorerModel)constructor.Invoke(services);
            ViewModel = new FileExplorerViewModel(Model,
                DispatchProxy.Create<IFavoriteDirectoriesViewModel, PassiveProxy>(),
                DispatchProxy.Create<IFilePreviewViewModel, PassiveProxy>(),
                DispatchProxy.Create<IFileExplorerFlatViewService, PassiveProxy>(),
                DispatchProxy.Create<IMessageService, PassiveProxy>(),
                DispatchProxy.Create<IFilePropertiesService, PassiveProxy>(),
                DispatchProxy.Create<IWindowManager, PassiveProxy>(),
                new WindowContext(new Dictionary<string, object?>()));
        }

        public async Task OpenAsync(string path)
        {
            await Manager.RefreshAsync();
            Assert.True(await Model.NavigateAsync(WpdLocatorCodec.Encode("phone", path).ToString(),
                FileExplorerNavigationMode.Standard), Model.NavigationErrorMessage);
        }

        public void Close()
        {
            if(closed)
                return;
            closed = true;
            ViewModel.Closed.Execute(null);
        }

        public void Dispose()
        {
            Close();
            Manager.Dispose();
            monitoring.Dispose();
            CryptoBook.Properties.Settings.Default.LastDirectory_FileExplorer = previousDirectory;
        }
    }

    public class PassiveProxy: DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Type type = targetMethod!.ReturnType;
            if(type == typeof(Task))
                return Task.CompletedTask;
            if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type resultType = type.GenericTypeArguments[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType)
                    .Invoke(null, [resultType.IsValueType ? Activator.CreateInstance(resultType) : null]);
            }
            return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }

    private sealed class ItemFactory(IDispatcherService dispatcher): ISystemItemCreateService
    {
        private readonly IDirectoryMonitoringService monitor = DispatchProxy.Create<IDirectoryMonitoringService, PassiveProxy>();
        private readonly ISystemItemSortService sort = new SystemItemSortService();
        public IDriveItem CreateRoot(string path) => new DriveItem(dispatcher, monitor, this, sort)
            { Name = path, FullPath = path, RootDirectory = path };
        public IDriveItem CreateRoot(StorageItemMetadata metadata)
        {
            IDriveItem item = CreateRoot(metadata.Location.ToString());
            Apply(item, metadata, null);
            return item;
        }
        public IDirectoryItem CreateDirectory(StorageItemMetadata metadata, ISystemItem parent)
        {
            var item = new DirectoryItem(dispatcher, monitor, this, sort);
            Apply(item, metadata, parent);
            return item;
        }
        public IFileItem CreateFile(StorageItemMetadata metadata, ISystemItem parent)
        {
            var item = new FileItem();
            Apply(item, metadata, parent);
            return item;
        }
        public IDirectoryItem CreateDirectory(string path, ISystemItem? parent) => throw new NotSupportedException();
        public IFileItem CreateFile(string path, ISystemItem? parent) => throw new NotSupportedException();
        private static void Apply(ISystemItem item, StorageItemMetadata metadata, ISystemItem? parent)
        {
            item.Name = metadata.Name;
            item.FullPath = metadata.Location.ToString();
            item.RootDirectory = parent?.RootDirectory ?? item.FullPath;
            item.Parent = parent;
            item.Capabilities = metadata.Capabilities;
            item.StatusText = metadata.StatusText;
        }
    }
}

using Autofac;

using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.ViewModels;

using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

[Collection("Settings UI")]
public sealed class FileExplorerProgressTests
{
    public FileExplorerProgressTests() => MemorySettingsProvider.Install();

    [WpfFact]
    public async Task ProgressReports_BurstAppliesLatestValueBeforeCompletion()
    {
        using var scope = new ContainerBuilder().Build();
        var viewModel = new ProgressViewModel(scope);
        viewModel.Prepare("test");
        var reporter = new ProgressDialogService.DispatcherProgressReporter(
            Dispatcher.CurrentDispatcher, viewModel,
            new StoragePathDisplayService(new StorageFacade([new LocalStorageProvider()])));
        int updates = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if(e.PropertyName == nameof(ProgressViewModel.Progress))
                updates++;
        };

        Task.Run(() =>
        {
            for(int index = 1; index <= 10_000; index++)
                reporter.Report(index / 10_000.0, $"file-{index}");
        }).GetAwaiter().GetResult();
        await reporter.CompleteAsync();
        reporter.Report(0, "late report");
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);

        Assert.Equal(100, viewModel.Progress);
        Assert.Equal("file-10000", viewModel.StatusMessage);
        Assert.Equal(1, updates);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_FlatListFinishesBeforeProgressCloses(bool cancel)
    {
        using var explorer = new ExplorerFixture();
        await explorer.LoadAsync();
        explorer.FlatView.GateNextScan = true;
        explorer.Recycle.CancelAfter = cancel ? 75 : int.MaxValue;
        ISystemItem[] selection = explorer.Root.Children.ToArray();

        explorer.Model.Execute_DeleteCommand(selection);
        await explorer.FlatView.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(explorer.Progress.Active);
        Assert.False(explorer.Progress.Completed.Task.IsCompleted);
        explorer.FlatView.ReleaseScan.TrySetResult();
        await explorer.Progress.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(explorer.Progress.Error);
        Assert.False(explorer.Progress.Active);
        string[] expected = Directory.GetFiles(explorer.Path);
        Assert.Equal(cancel ? 125 : 0, expected.Length);
        Assert.Equal(expected.Order(), explorer.Root.Children.Select(item => item.FullPath).Order());
        Assert.Equal(expected.Order(), explorer.ViewModel.ChildrenView.Cast<ISystemItem>().Select(item => item.FullPath).Order());
        Assert.Equal(expected.Order(), explorer.Model.FlatFilePathsSnapshot.Order());
        Assert.Contains(explorer.Progress.Messages, message => message ==
            CryptoBook.Infrastructure.LocalizationManager.GetString("Explorer.RefreshingAfterOperation"));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncryptionFailure_RefreshesPartialResultWhileProgressIsOpen(bool cancel)
    {
        using var explorer = new ExplorerFixture();
        await explorer.LoadAsync();
        explorer.FlatView.GateNextScan = true;
        explorer.EncryptAction = (items, token) =>
        {
            foreach(ISystemItem item in items.Take(75))
                File.Move(item.FullPath, item.FullPath + ".cbook");
            if(cancel)
            {
                explorer.Progress.Cancellation.Cancel();
                return Task.FromCanceled<FileOperationBatchResult>(token);
            }
            return Task.FromException<FileOperationBatchResult>(new IOException("simulated partial failure"));
        };

        explorer.Model.Execute_EncryptCommand(explorer.Root.Children.ToArray());
        await explorer.FlatView.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(explorer.Progress.Active);
        explorer.FlatView.ReleaseScan.TrySetResult();
        await explorer.Progress.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(explorer.Progress.Error);
        Assert.Equal(cancel, explorer.Progress.Error is OperationCanceledException);
        string[] expected = Directory.GetFiles(explorer.Path);
        Assert.Equal(75, expected.Count(path => path.EndsWith(".cbook")));
        Assert.Equal(expected.Order(), explorer.ViewModel.ChildrenView.Cast<ISystemItem>().Select(item => item.FullPath).Order());
        Assert.Equal(expected.Order(), explorer.Root.Children.Select(item => item.FullPath).Order());
    }

    private sealed class ExplorerFixture: IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
        public FileExplorerModel Model { get; }
        public FileExplorerViewModel ViewModel { get; }
        public DriveItem Root { get; }
        public ProgressStub Progress { get; } = new();
        public RecycleStub Recycle { get; }
        public FlatViewStub FlatView { get; }
        public Func<IReadOnlyList<ISystemItem>, CancellationToken, Task<FileOperationBatchResult>>? EncryptAction { get; set; }
        private readonly IFileManagerService files;

        public ExplorerFixture()
        {
            Directory.CreateDirectory(Path);
            for(int index = 0; index < 200; index++)
                File.WriteAllText(System.IO.Path.Combine(Path, $"file-{index:D4}.txt"), "test");
            var dispatcher = new WpfDispatcherService(Dispatcher.CurrentDispatcher);
            Root = new DriveItem(dispatcher, Stub<IDirectoryMonitoringService>(),
                Stub<ISystemItemCreateService>(), new SystemItemSortService())
            {
                FullPath = Path, RootDirectory = Path, IsLoaded = true,
                Capabilities = StorageProviderCapabilities.Read | StorageProviderCapabilities.Write | StorageProviderCapabilities.Delete
            };
            files = Stub<IFileManagerService>((method, _) => method.Name == "BrowseAsync"
                ? Task.FromResult(ReadFiles()) : null);
            FlatView = new FlatViewStub(ReadFiles);
            Recycle = new RecycleStub(Progress.Cancellation, FlatView);
            var coordinator = new FileOperationCoordinator(files, Progress,
                Stub<IFileConflictResolver>(), recycleBin: Recycle);
            var drives = new ReadOnlyObservableCollection<IDriveItem>(new ObservableCollection<IDriveItem> { Root });
            var services = new Dictionary<Type, object>
            {
                [typeof(IFileManagerService)] = files,
                [typeof(IDriveManagerService)] = Stub<IDriveManagerService>((method, _) =>
                    method.Name == "get_WritableDrives" ? drives : null),
                [typeof(IProgressDialogService)] = Progress,
                [typeof(IFileOperationCoordinator)] = coordinator,
                [typeof(IFileSecurityService)] = Stub<IFileSecurityService>((method, args) =>
                    method.Name == "EncryptAsync" && args![0] is IReadOnlyList<ISystemItem> items
                        ? EncryptAction!(items, (CancellationToken)args[^1]!) : null),
                [typeof(IMessageService)] = Stub<IMessageService>((method, _) => method.Name switch
                {
                    "ShowMessage" => Task.FromResult(Guid.NewGuid()),
                    "ShowConfirmation" => true,
                    _ => null
                })
            };
            ConstructorInfo constructor = Assert.Single(typeof(FileExplorerModel).GetConstructors());
            Model = (FileExplorerModel)constructor.Invoke(constructor.GetParameters().Select(parameter =>
                services.TryGetValue(parameter.ParameterType, out object? service) ? service :
                parameter.HasDefaultValue ? parameter.DefaultValue :
                DispatchProxy.Create(parameter.ParameterType, typeof(PassiveProxy))).ToArray());
            Model.SelectedItem = Root;
            typeof(FileExplorerModel).GetProperty(nameof(Model.CurrentPath))!.SetValue(Model, Path);
            ViewModel = new FileExplorerViewModel(Model,
                Stub<IFavoriteDirectoriesViewModel>(), Stub<IFilePreviewViewModel>(), FlatView,
                (IMessageService)services[typeof(IMessageService)], Stub<IFilePropertiesService>(),
                Stub<IWindowManager>(), new WindowContext(new Dictionary<string, object?>()));
        }

        private List<ISystemItem> ReadFiles() => Directory.GetFiles(Path).Select(path => (ISystemItem)new FileItem
        {
            Name = System.IO.Path.GetFileName(path), FullPath = path, Parent = Root,
            Extension = ".txt", Capabilities = Root.Capabilities
        }).ToList();

        public async Task LoadAsync()
        {
            await Root.AddChildAsync(ReadFiles(), item => item.FullPath);
            ViewModel.IsFlatViewEnabled = true;
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            Assert.Equal(200, ViewModel.ChildrenView.Cast<ISystemItem>().Count());
        }

        public void Dispose()
        {
            FlatView.ReleaseScan.TrySetResult();
            ViewModel.Closed.Execute(null);
            Progress.Cancellation.Dispose();
            Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class FlatViewStub(Func<List<ISystemItem>> readFiles): IFileExplorerFlatViewService
    {
        public event EventHandler? FilesChanged;
        public bool GateNextScan { get; set; }
        public TaskCompletionSource ScanStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseScan { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<FileExplorerFlatScanResult> ScanAsync(string rootPath, bool includeHidden, CancellationToken cancellationToken = default)
        {
            if(GateNextScan)
            {
                GateNextScan = false;
                ScanStarted.TrySetResult();
                await ReleaseScan.Task.WaitAsync(cancellationToken);
            }
            return new(readFiles().OfType<IFileItem>().ToArray(), 0);
        }
        public void NotifyChanged() => FilesChanged?.Invoke(this, EventArgs.Empty);
        public void StartMonitoring(string rootPath) { }
        public void StopMonitoring() { }
        public void Dispose() { }
    }

    private sealed class RecycleStub(CancellationTokenSource cancellation, FlatViewStub flatView): IRecycleBinService
    {
        public int CancelAfter { get; set; } = int.MaxValue;
        private int count;
        public Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default)
        {
            File.Delete(path);
            flatView.NotifyChanged();
            if(++count == CancelAfter)
                cancellation.Cancel();
            return Task.FromResult(FileOperationResult.Ok());
        }
    }

    private sealed class ProgressStub: IProgressDialogService, IProgressReporter
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public bool Active { get; private set; }
        public Exception? Error { get; private set; }
        public List<string?> Messages { get; } = [];
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<T> RunAsync<T>(string operationName, Func<IProgressReporter, CancellationToken, Task<T>> operation)
        {
            Active = true;
            try { return await operation(this, Cancellation.Token); }
            catch(Exception exception) { Error = exception; throw; }
            finally { Active = false; Completed.TrySetResult(); }
        }
        public void Report(double? value, string? currentInfo = null) => Messages.Add(currentInfo);
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?>? handler = null) where T: class
    {
        T proxy = DispatchProxy.Create<T, PassiveProxy>();
        ((PassiveProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class PassiveProxy: DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            object? handled = Handler?.Invoke(targetMethod!, args);
            if(handled is not null)
                return handled;
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
}

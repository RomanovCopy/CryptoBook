using Autofac;
using CryptoBook.AI;
using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Security;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;

namespace CryptoBook.Validation;

public sealed class IsolatedServices(string root, bool documentValidation = false, bool stage7 = false, bool hardwareAcceptance = false)
{
    public ILifetimeScope Container { get; private set; } = null!;
    public string Root { get; } = Path.GetFullPath(root);
    public string DemoDirectory => Path.Combine(Root, "Demo");
    public List<string> Paths { get; } = [];
    public ValidationSavePicker SavePicker { get; } = new();
    public ValidationDocumentDialogs DocumentDialogs { get; } = new();
    public ValidationImagePicker ImagePicker { get; } = new();
    public ValidationFolderPicker FolderPicker { get; } = new(Path.Combine(Path.GetFullPath(root), "Demo"));
    public ValidationRecycleBin RecycleBin { get; } = new(Path.GetFullPath(root));
    public ValidationPrintSink PrintSink { get; } = new();
    public Task PrepareDemoDriveAsync() => ((NoDriveMonitoring)Container.Resolve<IDriveManagerService>())
        .PrepareAsync(Container.Resolve<ISystemItemCreateService>(), DemoDirectory);
    public async Task PreparePortableDemoAsync(StorageItemMetadata device, StorageItemMetadata volume, StorageItemMetadata folder)
    {
        var factory = Container.Resolve<ISystemItemCreateService>();
        var root = factory.CreateRoot(device);
        var storage = factory.CreateDirectory(volume, root);
        var demo = factory.CreateDirectory(folder, storage);
        await root.AddChildAsync([storage], item => item.FullPath); root.IsLoaded = true;
        await storage.AddChildAsync([demo], item => item.FullPath); storage.IsLoaded = true;
        ((NoDriveMonitoring)Container.Resolve<IDriveManagerService>()).Add(root);
    }
    private string Store(string name)
    {
        string path = Path.GetFullPath(Path.Combine(Root, name));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Validation path escaped its root.");
        Paths.Add(path); return path;
    }
    public void Configure(ContainerBuilder builder)
    {
        Directory.CreateDirectory(DemoDirectory);
        builder.RegisterInstance(new AiSettingsStore(Store("ai-settings.json")));
        builder.RegisterInstance(new AiRuntimeProfileStore(Store("ai-profiles.json")));
        builder.RegisterInstance<IFavoriteDirectoryStore>(new JsonFavoriteDirectoryStore(Store("favorites.json")));
        builder.RegisterInstance<IPinnedDocumentStore>(new JsonPinnedDocumentStore(Store("pinned.json")));
        builder.RegisterInstance<IRecentDocumentStore>(new JsonRecentDocumentStore(Store("recent.json")));
        builder.RegisterInstance<IUpdateCheckStateStore>(new JsonUpdateCheckStateStore(Store("updates.json")));
        builder.RegisterInstance(new WorkspaceContentSearchOptions { IndexDirectory = Store("search-index") });
        string recovery = Store("Recovery/current.recovery"), snapshot = Store("Lock/last.lock.cbook");
        builder.Register(c => new DocumentRecoveryService(c.Resolve<IDocumentSession>(), c.Resolve<IRichTextBoxService>(),
            c.Resolve<IFlowDocumentSaveService>(), c.Resolve<IFlowDocumentLoadService>(), c.Resolve<IFileTemplateRegistry>(),
            c.Resolve<Dispatcher>(), recovery, autosaveFailureLogger: e => throw new InvalidOperationException("Recovery failed in validation.", e),
            markdownDocument: c.Resolve<IMarkdownDocumentState>(), keyProvider: c.Resolve<IKeyProvider>()))
            .As<IDocumentRecoveryService>().SingleInstance();
        builder.Register(c => new LockSnapshotService(c.Resolve<ISecureFileProcessor>(), c.Resolve<IFlowDocumentSaveService>(),
            c.Resolve<IFlowDocumentLoadService>(), snapshot, c.Resolve<IFileTemplateRegistry>(),
            c.Resolve<IMarkdownDocumentState>(), c.Resolve<IKeyProvider>())).As<ILockSnapshotService>().SingleInstance();
        if (!hardwareAcceptance) builder.RegisterInstance<IFolderPickerService>(FolderPicker);
        builder.RegisterInstance<IRecycleBinService>(RecycleBin);
        if (stage7) builder.RegisterInstance<IDocumentPrintService>(PrintSink);
        if (documentValidation)
        {
            builder.RegisterInstance<IDocumentSaveTargetPicker>(SavePicker);
            builder.RegisterInstance<IDocumentDialogService>(DocumentDialogs);
            builder.RegisterInstance<IImageFilePicker>(ImagePicker);
            builder.RegisterInstance<IDirectoryMonitoringService>(new NoDirectoryMonitoring());
        }
        builder.RegisterInstance<IApplicationUpdateService>(new NoUpdates());
        builder.RegisterInstance<IApplicationActivationService>(new NoActivation());
        if (!hardwareAcceptance) builder.RegisterInstance<IDriveManagerService>(new NoDriveMonitoring());
        // Theme, ink palette, geometry and help state use the installed memory provider.
        builder.RegisterBuildCallback(c => Container = c);
    }
    private sealed class NoUpdates : IApplicationUpdateService
    { public Task<ApplicationRelease?> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult<ApplicationRelease?>(null); }
    private sealed class NoDirectoryMonitoring : IDirectoryMonitoringService
    {
        public bool StartMonitoring(string directoryPath, Action<FileSystemEventArgs>? onCreated = null,
            Action<FileSystemEventArgs>? onDeleted = null, Action<RenamedEventArgs>? onRenamed = null,
            Action<FileSystemEventArgs>? onChanged = null, Action<Exception?>? onOverflowOrError = null,
            bool includeSubdirectories = false,
            NotifyFilters notifyFilters = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
            int internalBufferSize = 64 * 1024) => true;
        public bool StopMonitoring(string directoryPath) => true;
        public void Dispose() { }
    }
    private sealed class NoActivation : IApplicationActivationService
    {
        public Task<bool> StartAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public void NotifyMainWindowReady(Guid id) { }
        public void Dispose() { }
    }
    private sealed class NoDriveMonitoring : IDriveManagerService
    {
        private readonly ObservableCollection<IDriveItem> roots = [];
        public ReadOnlyObservableCollection<IDriveItem> WritableDrives { get; }
        public NoDriveMonitoring() => WritableDrives = new(roots);
        public void Add(IDriveItem root) => roots.Add(root);
        public async Task PrepareAsync(ISystemItemCreateService factory, string demoDirectory)
        {
            string drivePath = Path.GetPathRoot(demoDirectory)!;
            var root = factory.CreateRoot(drivePath); roots.Add(root);
            IContainerSystemItem current = root;
            foreach (string part in Path.GetRelativePath(drivePath, demoDirectory).Split(Path.DirectorySeparatorChar))
            {
                string nextPath = Path.Combine(current.FullPath.Replace("local://", ""), part);
                var next = factory.CreateDirectory(nextPath, current);
                await current.ClearChildrenAsync();
                await current.AddChildAsync([next], item => item.FullPath); current.IsLoaded = true;
                current = next;
            }
        }
        public event Action<IDriveItem> DriveConnected { add { } remove { } }
        public event Action<string> DriveDisconnected { add { } remove { } }
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void StartMonitoring() { }
        public void StopMonitoring() { }
        public void Dispose() { }
    }
}

// Deliberately records prepared content without opening a print dialog or sending a job.
public sealed class ValidationPrintSink : IDocumentPrintService
{
    public List<string> Texts { get; } = [];
    public void Print(System.Windows.Documents.FlowDocument document, string documentName) =>
        Texts.Add(new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Text);
}

public sealed class ValidationFolderPicker(string defaultPath) : IFolderPickerService
{
    public Queue<string?> Paths { get; } = new();
    public Task<string?> PickFolderAsync(string? initialDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string? path = Paths.Count > 0 ? Paths.Dequeue() : defaultPath;
        string root = Path.GetDirectoryName(defaultPath)! + Path.DirectorySeparatorChar;
        if (path is not null && !Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Validation folder picker escaped its disposable root.");
        return Task.FromResult(path);
    }
}

public sealed class ValidationImagePicker : IImageFilePicker
{
    public Queue<string?> Paths { get; } = new();
    public Task<string?> PickImageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Paths.Count > 0 ? Paths.Dequeue() : throw new InvalidOperationException("Unexpected image picker."));
}

// Only native modal decisions are supplied; the production save/open/close pipelines still execute.
public sealed class ValidationSavePicker : IDocumentSaveTargetPicker
{
    public Queue<DocumentSaveTarget?> Targets { get; } = new();
    public DocumentSaveTarget? Pick(string? currentFilePath, IFileTemplate? currentTemplate)
        => Targets.Count > 0 ? Targets.Dequeue() : throw new InvalidOperationException("Unexpected Save As dialog.");
}

public sealed class ValidationDocumentDialogs : IDocumentDialogService
{
    public Queue<UnsavedChangesChoice> Choices { get; } = new();
    public bool ConfirmRecovery() => false;
    public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() => Choices.Count > 0
        ? Choices.Dequeue() : throw new InvalidOperationException("Unexpected unsaved-changes prompt.");
    public void ShowRecoveryError(Exception exception) => throw new InvalidOperationException("Recovery failed.", exception);
    public void ShowRecoveryCleanupError(Exception exception) => throw new InvalidOperationException("Recovery cleanup failed.", exception);
}

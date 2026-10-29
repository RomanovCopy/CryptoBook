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

public sealed class IsolatedServices(string root)
{
    public ILifetimeScope Container { get; private set; } = null!;
    public string Root { get; } = Path.GetFullPath(root);
    public string DemoDirectory => Path.Combine(Root, "Demo");
    public List<string> Paths { get; } = [];
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
        builder.RegisterInstance<IFolderPickerService>(new DemoFolderPicker(DemoDirectory));
        builder.RegisterInstance<IApplicationUpdateService>(new NoUpdates());
        builder.RegisterInstance<IApplicationActivationService>(new NoActivation());
        builder.RegisterInstance<IDriveManagerService>(new NoDriveMonitoring());
        // Theme, ink palette, geometry and help state use the installed memory provider.
        builder.RegisterBuildCallback(c => Container = c);
    }
    private sealed class DemoFolderPicker(string path) : IFolderPickerService
    { public Task<string?> PickFolderAsync(string? initialDirectory, CancellationToken ct) => Task.FromResult<string?>(path); }
    private sealed class NoUpdates : IApplicationUpdateService
    { public Task<ApplicationRelease?> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult<ApplicationRelease?>(null); }
    private sealed class NoActivation : IApplicationActivationService
    {
        public Task<bool> StartAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public void NotifyMainWindowReady(Guid id) { }
        public void Dispose() { }
    }
    private sealed class NoDriveMonitoring : IDriveManagerService
    {
        public ReadOnlyObservableCollection<IDriveItem> WritableDrives { get; } = new(new ObservableCollection<IDriveItem>());
        public event Action<IDriveItem> DriveConnected { add { } remove { } }
        public event Action<string> DriveDisconnected { add { } remove { } }
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void StartMonitoring() { }
        public void StopMonitoring() { }
        public void Dispose() { }
    }
}

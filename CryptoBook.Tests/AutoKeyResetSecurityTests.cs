using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Xunit;

namespace CryptoBook.Tests;

public sealed class AutoKeyResetSecurityTests
{
    [WpfFact]
    public async Task MixedWorkspace_ResetClosesOnlyEncryptedDocument_RegardlessOfActiveEditor()
    {
        foreach(bool encryptedIsMarkdown in new[] { false, true })
        foreach(bool encryptedIsActive in new[] { false, true })
        {
            var editor = new RichTextBoxService(DispatchProxy.Create<IParagraphFactory, ParagraphProxy>(),
                DispatchProxy.Create<IUriNavigationService, UnusedProxy>(), new DocumentAppearanceDefaults());
            var markdown = new MarkdownDocumentState();
            var session = new DocumentSession(editor, markdown);
            var home = new FlowDocument(new Paragraph(new Run(encryptedIsMarkdown ? "ordinary home" : "protected home")));
            session.Open("home.dat", encryptedIsMarkdown ? new XamlPackageFileTemplate() : new SecureFileTemplate(), home);
            session.MarkDirty();
            var markdownDocument = new FlowDocument();
            MarkdownDocumentMetadata.SetSource(markdownDocument,
                new MarkdownTextDocument(encryptedIsMarkdown ? "protected markdown" : "ordinary markdown",
                    new System.Text.UTF8Encoding(false), []));
            session.Open("markdown.dat", encryptedIsMarkdown ? new SecureFileTemplate() : new MarkdownFileTemplate(), markdownDocument);
            session.MarkDirty();
            session.SelectPage(encryptedIsActive == encryptedIsMarkdown ? "MarkdownEditor" : "Home");
            using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
            key.SetKey("1234");
            var snapshot = new SuccessfulSnapshot();
            using var service = new AutoKeyResetService(key, snapshot, session, editor, new FileTemplateRegistry([]),
                new Lazy<IWorkspaceFileOpenService>(() => throw new InvalidOperationException()),
                new ImmediateDispatcher(), Application.Current ?? new Application());
            Exception? resetFailure = null;
            service.SnapshotFailed += (_, error) => resetFailure = error;
            Assert.True(await service.ResetAsync(), $"markdown={encryptedIsMarkdown}, active={encryptedIsActive}: {resetFailure}");
            Assert.False(key.HasKey);
            Assert.True(session.HasDocument);
            Assert.True(session.IsDirty);
            Assert.IsNotType<SecureFileTemplate>(session.Template);
            Assert.Equal(encryptedIsMarkdown ? "Home" : "MarkdownEditor", session.ActivePageKey);
            Assert.Null(session.CaptureInactiveDocument());
            Assert.Equal("Encrypted file", snapshot.Metadata!.TemplateId);
            Assert.Null(snapshot.Metadata.InactiveDocument);
            Assert.False(Behaviors.WorkspaceLockPresentation.IsLocked(service.State, service.HasRetainedDocument));
            if(encryptedIsMarkdown)
                Assert.Contains("ordinary home", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            else
                Assert.Equal("ordinary markdown", markdown.Text);
        }
    }

    public class ParagraphProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => new ParagraphService();
    }

    private sealed class SuccessfulSnapshot : ILockSnapshotService
    {
        public string SnapshotPath => "unused";
        public bool Exists => Metadata is not null;
        public LockSnapshotMetadata? Metadata { get; private set; }
        public Task CreateAndVerifyAsync(IRichTextBoxService editor, LockSnapshotMetadata metadata,
            CancellationToken cancellationToken = default)
        { Metadata = metadata; return Task.CompletedTask; }
        public Task<(FlowDocument Document, LockSnapshotMetadata Metadata)> ReadAndVerifyAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Delete() { }
    }

    [WpfFact]
    public async Task PlainDocument_KeyResetDoesNotTouchDocumentOrCreateProtectedSnapshot()
    {
        using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
        key.SetKey("1234");
        var session = new SessionStub { Template = new PlainTextTemplate() };
        var snapshot = new FailingSnapshot();
        using var service = new AutoKeyResetService(key, snapshot, session,
            DispatchProxy.Create<IRichTextBoxService, UnusedProxy>(), new FileTemplateRegistry([]),
            new Lazy<IWorkspaceFileOpenService>(() => throw new InvalidOperationException()),
            new ImmediateDispatcher(), Application.Current ?? new Application());
        service.Start();
        Assert.True(await service.ResetAsync());
        Assert.False(key.HasKey);
        Assert.True(session.HasDocument);
        Assert.Equal(KeyResetState.Inactive, service.State);
        Assert.False(service.HasRetainedDocument);
        Assert.Equal(0, snapshot.CreateCount);
    }

    [WpfFact]
    public void StartupWithPendingSnapshot_AllowsNormalKeyEntryWithoutUnlockingApplication()
    {
        using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
        using var service = new AutoKeyResetService(key, new FailingSnapshot { Exists = true },
            new SessionStub { Template = new PlainTextTemplate() },
            DispatchProxy.Create<IRichTextBoxService, UnusedProxy>(), new FileTemplateRegistry([]),
            new Lazy<IWorkspaceFileOpenService>(() => throw new InvalidOperationException()),
            new ImmediateDispatcher(), Application.Current ?? new Application());
        service.Start();
        Assert.False(key.HasKey);
        Assert.False(Behaviors.WorkspaceLockPresentation.IsLocked(service.State, service.HasRetainedDocument));
        var manager = DispatchProxy.Create<IWindowManager, KeyEntryProxy>();
        ((KeyEntryProxy)(object)manager).Key = key;
        var request = new EncryptionKeyRequestService(key, manager, new Lazy<IKeyResetService>(() => service));
        Assert.True(request.EnsureKeyAvailable());
        service.NotifyActivity();
        Assert.Equal(KeyResetState.Active, service.State);
    }

    public class KeyEntryProxy : DispatchProxy
    {
        public IKeyProvider Key { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if(method!.Name == "CreateWindow") return Guid.NewGuid();
            if(method.Name == "ShowWindowDialog") { Key.SetKey("another file password"); return null; }
            if(method.Name == "GetResult") return true;
            throw new InvalidOperationException(method.Name);
        }
    }

    [WpfFact]
    public async Task SnapshotFailure_RevokesKeyAndKeepsUnsavedDocumentLockedUntilCorrectPassword()
    {
        using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
        key.SetKey("1234");
        var session = new SessionStub();
        var snapshot = new FailingSnapshot();
        var app = Application.Current ?? new Application();
        using var service = new AutoKeyResetService(key, snapshot, session,
            DispatchProxy.Create<IRichTextBoxService, UnusedProxy>(), new FileTemplateRegistry([]),
            new Lazy<IWorkspaceFileOpenService>(() => throw new InvalidOperationException()),
            new ImmediateDispatcher(), app);
        var states = new List<KeyResetState>();
        service.StateChanged += (_, args) => states.Add(args.State);
        Assert.False(await service.ResetAsync());
        Assert.Equal(KeyResetState.KeyReset, service.State);
        Assert.False(key.HasKey);
        Assert.True(session.HasDocument);
        Assert.True(service.HasRetainedDocument);
        Assert.Equal(new[] { KeyResetState.Resetting, KeyResetState.KeyReset }, states);
        Assert.False(await service.TryUnlockAsync("wrong".AsMemory()));
        Assert.False(key.HasKey);
        Assert.Equal(KeyResetState.KeyReset, service.State);
        await Task.Delay(550);
        Assert.True(await service.TryUnlockAsync("1234".AsMemory()));
        Assert.Equal(KeyResetState.Active, service.State);
        Assert.True(key.HasKey);
        Assert.True(session.HasDocument);
        Assert.False(service.HasRetainedDocument);
        Assert.Equal(0, snapshot.ReadCount);
    }

    [WpfFact]
    public async Task CancellationDuringSnapshot_DoesNotReopenWorkspace()
    {
        using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
        key.SetKey("old-key");
        var session = new SessionStub();
        using var service = new AutoKeyResetService(key,
            new FailingSnapshot { Failure = new OperationCanceledException() }, session,
            DispatchProxy.Create<IRichTextBoxService, UnusedProxy>(), new FileTemplateRegistry([]),
            new Lazy<IWorkspaceFileOpenService>(() => throw new InvalidOperationException()),
            new ImmediateDispatcher(), Application.Current ?? new Application());
        Assert.False(await service.ResetAsync());
        Assert.Equal(KeyResetState.KeyReset, service.State);
        Assert.False(key.HasKey);
        Assert.True(await service.TryUnlockAsync("old-key".AsMemory()));
        Assert.True(session.HasDocument);
    }

    private sealed class FailingSnapshot : ILockSnapshotService
    {
        public Exception Failure { get; init; } = new IOException("synthetic disk failure");
        public int ReadCount { get; private set; }
        public int CreateCount { get; private set; }
        public string SnapshotPath => "unused";
        public bool Exists { get; init; }
        public Task CreateAndVerifyAsync(IRichTextBoxService editor, LockSnapshotMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return Task.FromException(Failure);
        }
        public Task<(FlowDocument Document, LockSnapshotMetadata Metadata)> ReadAndVerifyAsync(
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            throw new InvalidOperationException("No disk snapshot exists.");
        }
        public void Delete() { }
    }

    private sealed class SessionStub : IDocumentSession
    {
        public string? FilePath => "document.cbook";
        public string DisplayName => "document";
        public IFileTemplate? Template { get; init; } = new SecureFileTemplate();
        public bool IsDirty => true;
        public long Revision => 1;
        public long SavedRevision => 0;
        public bool HasDocument { get; private set; } = true;
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public void Close() => HasDocument = false;
        public void Open(string path, IFileTemplate template) => throw new NotSupportedException();
        public void Open(string path, IFileTemplate template, FlowDocument document) => throw new NotSupportedException();
        public void MarkDirty() { }
        public void MarkSaved(string path, IFileTemplate template) { }
        public void MarkSaved(string path, IFileTemplate template, long revision) { }
        public void Rename(string path) { }
        public void SetDisplayName(string value) { }
    }

    public class UnusedProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("Unexpected editor access");
    }

    private sealed class ImmediateDispatcher : IDispatcherService
    {
        public bool CheckAccess() => true;
        public void Invoke(Action action) => action();
        public void BeginInvoke(Action action) => action();
        public Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> action, DispatcherPriority priority = DispatcherPriority.Background) =>
            Task.FromResult(action());
    }
}

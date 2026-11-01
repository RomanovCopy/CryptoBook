using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Security;
using System.Security.Cryptography;

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class DocumentRecoveryTests
    {
        [WpfFact]
        public async Task DeferredRecovery_UsesNewestCopy_AndPreservesOlderCopyUntilItIsChosen()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var editor = CreateEditor(); var session = new DocumentSession(editor);
                var template = new XamlPackageFileTemplate();
                using var recovery = new DocumentRecoveryService(session, editor, new TestSaveService(),
                    new TestLoadService(), new FileTemplateRegistry([template]), Dispatcher.CurrentDispatcher,
                    Path.Combine(directory, "current.recovery"));
                foreach(string text in new[] { "older work", "newer work" })
                {
                    session.Open(Path.Combine(directory, "document.XamlPackage"), template,
                        new FlowDocument(new Paragraph(new Run(text)))); session.MarkDirty();
                    await recovery.SaveSnapshotNowAsync(); await recovery.DeferSnapshotAsync();
                    string latest = Directory.GetFiles(directory, "*.deferred").OrderByDescending(File.GetLastWriteTimeUtc).First();
                    File.SetLastWriteTimeUtc(latest, text == "older work" ? DateTime.UtcNow.AddDays(-2) : DateTime.UtcNow.AddDays(-1));
                }
                session.Close();
                Assert.NotNull(recovery.DeferredSnapshotTime);
                Assert.True(await recovery.RestoreDeferredSnapshotAsync());
                Assert.Contains("newer work", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
                Assert.True(session.IsDirty); Assert.True(recovery.HasSnapshot);
                Assert.Single(Directory.GetFiles(directory, "*.deferred"));
                await recovery.StopAsync();
                Assert.True(await recovery.RestoreDeferredSnapshotAsync());
                Assert.Contains("older work", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
                Assert.Null(recovery.DeferredSnapshotTime);
                await recovery.StopAsync();
            }
            finally { Directory.Delete(directory, true); }
        }

        [WpfFact]
        public async Task DeferredRecovery_WrongOrMissingKey_PreservesCopyAndCurrentEditor()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var editor = CreateEditor(); var session = new DocumentSession(editor);
                var plain = new XamlPackageFileTemplate(); var secure = new SecureFileTemplate();
                using var key = new MemoryKeyProvider(new Argon2idKeyDeriver()); key.SetKey("original demo key!9");
                using var recovery = new DocumentRecoveryService(session, editor, new TestSaveService(), new TestLoadService(),
                    new FileTemplateRegistry([plain, secure]), Dispatcher.CurrentDispatcher, Path.Combine(directory, "current.recovery"), keyProvider: key);
                session.Open(Path.Combine(directory, "protected.cbook"), secure, new FlowDocument(new Paragraph(new Run("protected unsaved work")))); session.MarkDirty();
                await recovery.SaveSnapshotNowAsync(); await recovery.DeferSnapshotAsync();
                string archived = Directory.GetFiles(directory, "*.deferred").Single(); byte[] bytes = File.ReadAllBytes(archived);
                string currentPath = Path.Combine(directory, "current.XamlPackage");
                session.Open(currentPath, plain, new FlowDocument(new Paragraph(new Run("keep current work")))); session.MarkDirty();
                key.Clear(); Assert.False(await recovery.RestoreDeferredSnapshotAsync());
                key.SetKey("wrong demo key!8");
                await Assert.ThrowsAnyAsync<CryptographicException>(() => recovery.RestoreDeferredSnapshotAsync());
                Assert.False(key.HasKey); Assert.Equal(currentPath, session.FilePath);
                Assert.Contains("keep current work", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
                Assert.Equal(bytes, File.ReadAllBytes(archived));
                await recovery.StopAsync();
            }
            finally { Directory.Delete(directory, true); }
        }

        [WpfFact]
        public async Task DeferredRecovery_FailedReplacementCrashCopy_PreservesArchivedCopy()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var editor = CreateEditor(); var session = new DocumentSession(editor); var template = new XamlPackageFileTemplate();
                string path = Path.Combine(directory, "current.recovery");
                using var recovery = new DocumentRecoveryService(session, editor, new TestSaveService(), new TestLoadService(),
                    new FileTemplateRegistry([template]), Dispatcher.CurrentDispatcher, path);
                session.Open(Path.Combine(directory, "document.XamlPackage"), template,
                    new FlowDocument(new Paragraph(new Run("durable previous work")))); session.MarkDirty();
                await recovery.SaveSnapshotNowAsync(); await recovery.DeferSnapshotAsync();
                string archived = Directory.GetFiles(directory, "*.deferred").Single(); byte[] bytes = File.ReadAllBytes(archived);
                session.Close(); Directory.CreateDirectory(path);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => recovery.RestoreDeferredSnapshotAsync());
                Assert.Equal(bytes, File.ReadAllBytes(archived));
                Assert.Contains("durable previous work", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
                await recovery.StopAsync();
            }
            finally { Directory.Delete(directory, true); }
        }

        [WpfFact]
        public async Task DeferredRecovery_ConcurrentSnapshotCleanup_DoesNotConsumeArchivedCopy()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var editor = CreateEditor(); var session = new DocumentSession(editor); var template = new XamlPackageFileTemplate();
                var save = new GatedSaveService();
                using var recovery = new DocumentRecoveryService(session, editor, save, new TestLoadService(),
                    new FileTemplateRegistry([template]), Dispatcher.CurrentDispatcher, Path.Combine(directory, "current.recovery"));
                session.Open(Path.Combine(directory, "document.XamlPackage"), template,
                    new FlowDocument(new Paragraph(new Run("retain until committed")))); session.MarkDirty();
                await recovery.SaveSnapshotNowAsync(); await recovery.DeferSnapshotAsync();
                string archived = Directory.GetFiles(directory, "*.deferred").Single(); byte[] original = File.ReadAllBytes(archived);
                session.Close(); save.Block = true;
                Task<bool> restore = recovery.RestoreDeferredSnapshotAsync();
                await save.Started.Task;
                Task cleanup = recovery.DeleteSnapshotAsync();
                save.Continue.TrySetResult();
                await Assert.ThrowsAsync<IOException>(() => restore);
                await cleanup;
                Assert.Equal(original, File.ReadAllBytes(archived));
                Assert.Contains("retain until committed", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
                await recovery.StopAsync();
            }
            finally { Directory.Delete(directory, true); }
        }

        [WpfFact]
        public async Task PlainRecovery_DoesNotDependOnCachedEncryptionPassword_AndCanBeDeferred()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var editor = CreateEditor();
                var session = new DocumentSession(editor);
                var template = new XamlPackageFileTemplate();
                var registry = new FileTemplateRegistry([template]);
                session.Open(Path.Combine(directory, "plain.XamlPackage"), template,
                    new FlowDocument(new Paragraph(new Run("ordinary unsaved work"))));
                session.MarkDirty();
                using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
                key.SetKey("cached encryption password");
                string path = Path.Combine(directory, "current.recovery");
                using(var recovery = new DocumentRecoveryService(session, editor,
                    new TestSaveService(), new TestLoadService(), registry,
                    Dispatcher.CurrentDispatcher, path, keyProvider: key))
                {
                    await recovery.SaveSnapshotNowAsync();
                    Assert.False(PasswordProtectedBuffer.HasHeader(await File.ReadAllBytesAsync(path)));
                    await recovery.DeferSnapshotAsync();
                    Assert.False(recovery.HasSnapshot);
                    // Ordinary work can save/close without deleting the deferred copy.
                    await recovery.DeleteSnapshotAsync();
                    Assert.Single(Directory.GetFiles(directory, "*.deferred"));
                }
                key.Clear();
                session.Close();
                using var reopened = new DocumentRecoveryService(session, editor,
                    new TestSaveService(), new TestLoadService(), registry,
                    Dispatcher.CurrentDispatcher, path, keyProvider: key);
                Assert.True(reopened.HasSnapshot);
                Assert.True(await reopened.RestoreSnapshotAsync());
                Assert.Contains("ordinary unsaved work", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
                Assert.False(key.HasKey);
            }
            finally { Directory.Delete(directory, recursive: true); }
        }

        [WpfFact]
        public async Task ProtectedSnapshot_RequiresCryptoBookPassword_EvenInSameWindowsAccount()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var editor = CreateEditor();
                var session = new DocumentSession(editor);
                var template = new SecureFileTemplate();
                session.Open(Path.Combine(directory, "source.cbook"), template,
                    new FlowDocument(new Paragraph(new Run("confidential recovery contents"))));
                session.MarkDirty();
                using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
                // Existing weak passwords must still protect recovery without losing edits.
                key.SetKey("1234");
                string path = Path.Combine(directory, "current.recovery");
                using var recovery = new DocumentRecoveryService(session, editor,
                    new TestSaveService(), new TestLoadService(), new FileTemplateRegistry([template]),
                    Dispatcher.CurrentDispatcher, path, keyProvider: key);
                await recovery.SaveSnapshotNowAsync();
                byte[] snapshot = await File.ReadAllBytesAsync(path);
                Assert.True(PasswordProtectedBuffer.HasHeader(snapshot));
                Assert.Throws<CryptographicException>(() => ProtectedData.Unprotect(snapshot,
                    Encoding.UTF8.GetBytes("CryptoBook.DocumentRecovery.v1"), DataProtectionScope.CurrentUser));
                session.Close();
                key.Clear();
                Assert.False(await recovery.RestoreSnapshotAsync());
                Assert.False(session.HasDocument);
                key.SetKey("wrong");
                await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => recovery.RestoreSnapshotAsync());
                Assert.False(session.HasDocument);
                key.SetKey("1234");
                Assert.True(await recovery.RestoreSnapshotAsync());
                Assert.Contains("confidential recovery contents",
                    new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            }
            finally { Directory.Delete(directory, recursive: true); }
        }

        [WpfFact]
        public async Task ProtectedSessionWithoutKey_DoesNotFallBackToDpapi()
        {
            string path = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"), "current.recovery");
            var editor = CreateEditor();
            var session = new DocumentSession(editor);
            var template = new SecureFileTemplate();
            session.Open("protected.cbook", template);
            session.MarkDirty();
            using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
            using var recovery = new DocumentRecoveryService(session, editor,
                new TestSaveService(), new TestLoadService(), new FileTemplateRegistry([template]),
                Dispatcher.CurrentDispatcher, path, keyProvider: key);
            await Assert.ThrowsAsync<CryptographicException>(() => recovery.SaveSnapshotNowAsync());
            Assert.False(File.Exists(path));
        }

        [WpfFact]
        public async Task Snapshot_CleanHomeAndDirtyInactiveMarkdown_RestoresBoth()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var editor = CreateEditor();
                var markdown = new MarkdownDocumentState();
                var session = new DocumentSession(editor, markdown);
                var template = new XamlPackageFileTemplate();
                var registry = new FileTemplateRegistry([template, new MarkdownFileTemplate()]);
                session.Open(Path.Combine(directory, "home.XamlPackage"), template,
                    new FlowDocument(new Paragraph(new Run("Home contents"))));
                var markdownDocument = new FlowDocument();
                MarkdownDocumentMetadata.SetSource(markdownDocument,
                    new MarkdownTextDocument("# Original", new UTF8Encoding(false), []));
                session.Open(Path.Combine(directory, "book.md"), new MarkdownFileTemplate(), markdownDocument);
                markdown.Text = "# Unsaved independent Markdown";
                session.SelectPage("Home");
                Assert.False(session.IsDirty);
                Assert.True(session.HasInactiveChanges);
                using var recovery = new DocumentRecoveryService(session, editor,
                    new TestSaveService(), new TestLoadService(), registry,
                    Dispatcher.CurrentDispatcher, Path.Combine(directory, "current.recovery"),
                    markdownDocument: markdown);
                await recovery.SaveSnapshotNowAsync();
                Assert.True(recovery.HasSnapshot);
                session.Close();
                Assert.True(await recovery.RestoreSnapshotAsync());
                Assert.Contains("Home contents", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
                Assert.True(session.HasMarkdownDocument);
                session.SelectPage("MarkdownEditor");
                Assert.Equal("# Unsaved independent Markdown", markdown.Text);
                Assert.True(session.IsDirty);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [WpfFact]
        public async Task Snapshot_IsEncrypted_AndRestoresDirtyDocument()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string recoveryPath =
                Path.Combine(directory, "current.recovery");
            string originalPath =
                Path.Combine(directory, "book.XamlPackage");
            var template = new XamlPackageFileTemplate();
            var registry = new FileTemplateRegistry([template]);

            try
            {
                IRichTextBoxService sourceEditor = CreateEditor();
                var sourceSession = new DocumentSession(sourceEditor);
                sourceSession.Open(originalPath, template);
                sourceEditor.Selection.Text =
                    "Секретный текст восстановления";

                using(var recovery = new DocumentRecoveryService(
                    sourceSession,
                    sourceEditor,
                    new TestSaveService(),
                    new TestLoadService(),
                    registry,
                    Dispatcher.CurrentDispatcher,
                    recoveryPath))
                {
                    await recovery.SaveSnapshotNowAsync();
                }

                Assert.True(File.Exists(recoveryPath));
                string rawFile = Encoding.UTF8.GetString(
                    await File.ReadAllBytesAsync(recoveryPath));
                Assert.DoesNotContain(
                    "Секретный текст восстановления",
                    rawFile);
                Assert.DoesNotContain(originalPath, rawFile);

                IRichTextBoxService restoredEditor = CreateEditor();
                var restoredSession = new DocumentSession(restoredEditor);
                using var restore = new DocumentRecoveryService(
                    restoredSession,
                    restoredEditor,
                    new TestSaveService(),
                    new TestLoadService(),
                    registry,
                    Dispatcher.CurrentDispatcher,
                    recoveryPath);

                Assert.True(await restore.RestoreSnapshotAsync());
                Assert.Contains(
                    "Секретный текст восстановления",
                    new TextRange(
                        restoredEditor.Document.ContentStart,
                        restoredEditor.Document.ContentEnd).Text);
                Assert.Equal(
                    Path.GetFullPath(originalPath),
                    restoredSession.FilePath);
                Assert.True(restoredSession.IsDirty);
            }
            finally
            {
                if(Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [WpfFact]
        public async Task DeleteSnapshotAsync_Throws_WhenSnapshotIsLocked()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string recoveryPath =
                Path.Combine(directory, "current.recovery");
            await File.WriteAllTextAsync(recoveryPath, "snapshot");

            try
            {
                IRichTextBoxService editor = CreateEditor();
                using var recovery = new DocumentRecoveryService(
                    new DocumentSession(editor),
                    editor,
                    new TestSaveService(),
                    new TestLoadService(),
                    new FileTemplateRegistry([]),
                    Dispatcher.CurrentDispatcher,
                    recoveryPath);
                await using(FileStream lockedFile = new(
                    recoveryPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None))
                {
                    await Assert.ThrowsAsync<IOException>(
                        recovery.DeleteSnapshotAsync);
                }

                Assert.True(File.Exists(recoveryPath));
                await recovery.DeleteSnapshotAsync();
                Assert.False(File.Exists(recoveryPath));
            }
            finally
            {
                if(Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [WpfFact]
        public async Task DeleteSnapshotAsync_Retries_AfterTemporaryLock()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string recoveryPath =
                Path.Combine(directory, "current.recovery");
            await File.WriteAllTextAsync(recoveryPath, "snapshot");

            try
            {
                IRichTextBoxService editor = CreateEditor();
                using var recovery = new DocumentRecoveryService(
                    new DocumentSession(editor),
                    editor,
                    new TestSaveService(),
                    new TestLoadService(),
                    new FileTemplateRegistry([]),
                    Dispatcher.CurrentDispatcher,
                    recoveryPath);
                FileStream lockedFile = new(
                    recoveryPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);
                Task releaseLock = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await lockedFile.DisposeAsync();
                });

                await recovery.DeleteSnapshotAsync();
                await releaseLock;

                Assert.False(File.Exists(recoveryPath));
            }
            finally
            {
                if(Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [WpfFact]
        public async Task DeleteSnapshotAsync_Succeeds_WhenDirectoryDoesNotExist()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            string recoveryPath =
                Path.Combine(directory, "current.recovery");
            IRichTextBoxService editor = CreateEditor();
            using var recovery = new DocumentRecoveryService(
                new DocumentSession(editor),
                editor,
                new TestSaveService(),
                new TestLoadService(),
                new FileTemplateRegistry([]),
                Dispatcher.CurrentDispatcher,
                recoveryPath);

            await recovery.DeleteSnapshotAsync();

            Assert.False(Directory.Exists(directory));
        }

        [WpfFact]
        public async Task StopAsync_WaitsForActiveSave_AndPreventsSnapshotCommit()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string recoveryPath =
                Path.Combine(directory, "current.recovery");

            try
            {
                IRichTextBoxService editor = CreateEditor();
                var session = new DocumentSession(editor);
                session.MarkDirty();
                var saveService = new BlockingSaveService();
                using var recovery = new DocumentRecoveryService(
                    session,
                    editor,
                    saveService,
                    new TestLoadService(),
                    new FileTemplateRegistry([]),
                    Dispatcher.CurrentDispatcher,
                    recoveryPath);

                Task snapshot = recovery.SaveSnapshotNowAsync();
                await saveService.Started.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));

                Task stop = recovery.StopAsync();
                Assert.False(stop.IsCompleted);
                saveService.Continue.TrySetResult();

                await Task.WhenAll(snapshot, stop);

                Assert.False(File.Exists(recoveryPath));
            }
            finally
            {
                if(Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [WpfFact]
        public void AutosaveFailureLogging_IsRateLimited()
        {
            string recoveryPath = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"),
                "current.recovery");
            DateTimeOffset now =
                new(2026, 7, 31, 9, 0, 0, TimeSpan.Zero);
            var loggedExceptions = new List<Exception>();
            IRichTextBoxService editor = CreateEditor();
            using var recovery = new DocumentRecoveryService(
                new DocumentSession(editor),
                editor,
                new TestSaveService(),
                new TestLoadService(),
                new FileTemplateRegistry([]),
                Dispatcher.CurrentDispatcher,
                recoveryPath,
                loggedExceptions.Add,
                () => now);

            recovery.LogAutosaveFailure(new IOException("first"));
            now = now.AddMinutes(4);
            recovery.LogAutosaveFailure(new IOException("suppressed"));
            now = now.AddMinutes(1);
            recovery.LogAutosaveFailure(new IOException("second"));

            Assert.Collection(
                loggedExceptions,
                exception => Assert.Equal("first", exception.Message),
                exception => Assert.Equal("second", exception.Message));
        }

        private static IRichTextBoxService CreateEditor() =>
            new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService(),
                new DocumentAppearanceDefaults());

        private sealed class GatedSaveService : IFlowDocumentSaveService
        {
            public bool Block { get; set; }
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task SaveToFileAsync(IRichTextBoxService editor, string path, IFileTemplate template,
                CancellationToken cancellationToken = default, IProgressReporter? progress = null,
                FlowDocument? documentSnapshot = null) => throw new NotSupportedException();
            public async Task SaveToStreamAsync(IRichTextBoxService editor, Stream destination, IFileTemplate template,
                CancellationToken cancellationToken = default, IProgressReporter? progress = null,
                FlowDocument? documentSnapshot = null)
            {
                if(Block) { Started.TrySetResult(); await Continue.Task.WaitAsync(cancellationToken); }
                await new TestSaveService().SaveToStreamAsync(editor, destination, template, cancellationToken, progress, documentSnapshot);
            }
        }

        private sealed class TestSaveService: IFlowDocumentSaveService
        {
            public Task SaveToFileAsync(
                IRichTextBoxService richTextBoxService,
                string filePath,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null,
                FlowDocument? documentSnapshot = null)
            {
                throw new NotSupportedException();
            }

            public Task SaveToStreamAsync(
                IRichTextBoxService richTextBoxService,
                Stream destination,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null,
                FlowDocument? documentSnapshot = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                new TextRange(
                    richTextBoxService.Document.ContentStart,
                    richTextBoxService.Document.ContentEnd).Save(
                        destination,
                        DataFormats.Xaml,
                        preserveTextElements: true);
                return Task.CompletedTask;
            }
        }

        private sealed class TestLoadService: IFlowDocumentLoadService
        {
            public async Task<FlowDocument> PrepareAsync(
                Stream source,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = new FlowDocument();
                new TextRange(
                    document.ContentStart,
                    document.ContentEnd).Load(
                        source,
                        DataFormats.Xaml);
                await Task.CompletedTask;
                return document;
            }

            public Task LoadAsync(
                IRichTextBoxService richTextBoxService,
                Stream source,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                new TextRange(
                    richTextBoxService.Document.ContentStart,
                    richTextBoxService.Document.ContentEnd).Load(
                        source,
                        DataFormats.Xaml);
                return Task.CompletedTask;
            }
        }

        private sealed class BlockingSaveService: IFlowDocumentSaveService
        {
            public TaskCompletionSource Started { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Continue { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public Task SaveToFileAsync(
                IRichTextBoxService richTextBoxService,
                string filePath,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null,
                FlowDocument? documentSnapshot = null) =>
                throw new NotSupportedException();

            public async Task SaveToStreamAsync(
                IRichTextBoxService richTextBoxService,
                Stream destination,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null,
                FlowDocument? documentSnapshot = null)
            {
                Started.TrySetResult();
                await Continue.Task.WaitAsync(cancellationToken);
                await destination.WriteAsync(
                    Encoding.UTF8.GetBytes("snapshot"),
                    cancellationToken);
            }
        }

        private sealed class TestParagraphFactory: IParagraphFactory
        {
            public IParagraphService Create(Inline? inline = null)
            {
                var paragraph = new ParagraphService();
                if(inline is not null)
                    paragraph.Inlines.Add(inline);
                return paragraph;
            }
        }
    }
}

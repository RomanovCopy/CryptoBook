using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;

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
                new TestUriNavigationService());

        private sealed class TestSaveService: IFlowDocumentSaveService
        {
            public Task SaveToFileAsync(
                IRichTextBoxService richTextBoxService,
                string filePath,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null)
            {
                throw new NotSupportedException();
            }

            public Task SaveToStreamAsync(
                IRichTextBoxService richTextBoxService,
                Stream destination,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null)
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

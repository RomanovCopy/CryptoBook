using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Windows.Documents;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class WorkspaceInternalFileOpenServiceTests: IDisposable
    {
        private readonly string testDirectory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));

        public WorkspaceInternalFileOpenServiceTests()
        {
            Directory.CreateDirectory(testDirectory);
        }

        [WpfFact]
        public async Task PreparationFailure_LeavesEditorAndSessionUntouched()
        {
            IRichTextBoxService richTextBox = CreateRichTextBox();
            var session = new DocumentSession(richTextBox);
            var currentTemplate = new PlainTextTemplate();
            string currentPath = Path.Combine(testDirectory, "current.txt");
            string targetPath = Path.Combine(testDirectory, "broken.txt");
            await File.WriteAllTextAsync(targetPath, "broken");
            session.Open(currentPath, currentTemplate);
            richTextBox.Selection.Text = "несохранённый текст";
            long currentRevision = session.Revision;
            var service = CreateService(
                richTextBox,
                session,
                new LoadServiceStub
                {
                    Exception = new InvalidDataException("damaged")
                });

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.OpenDocumentAsync(
                    targetPath,
                    targetPath,
                    new PlainTextTemplate(),
                    sourceIsEncrypted: false));

            Assert.Equal(Path.GetFullPath(currentPath), session.FilePath);
            Assert.Equal(currentRevision, session.Revision);
            Assert.True(session.IsDirty);
            Assert.Contains(
                "несохранённый текст",
                new TextRange(
                    richTextBox.Document.ContentStart,
                    richTextBox.Document.ContentEnd).Text);
        }

        [WpfFact]
        public async Task PreparedEncryptedDocument_IsCommittedWithSourceIdentity()
        {
            IRichTextBoxService richTextBox = CreateRichTextBox();
            var session = new DocumentSession(richTextBox);
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            string decryptedPath = Path.Combine(testDirectory, "secret.txt");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            await File.WriteAllTextAsync(decryptedPath, "payload");
            var service = CreateService(
                richTextBox,
                session,
                new LoadServiceStub { Content = "защищённый текст" });

            await service.OpenDocumentAsync(
                sourcePath,
                decryptedPath,
                new PlainTextTemplate(),
                sourceIsEncrypted: true);

            Assert.Equal(Path.GetFullPath(sourcePath), session.FilePath);
            Assert.IsType<SecureFileTemplate>(session.Template);
            Assert.False(session.IsDirty);
            Assert.Contains(
                "защищённый текст",
                new TextRange(
                    richTextBox.Document.ContentStart,
                    richTextBox.Document.ContentEnd).Text);
        }

        private static WorkspaceInternalFileOpenService CreateService(
            IRichTextBoxService richTextBox,
            IDocumentSession session,
            IFlowDocumentLoadService loadService)
        {
            IFileTemplateRegistry registry = new FileTemplateRegistry(
            [
                new PlainTextTemplate(),
                new SecureFileTemplate()
            ]);
            return new WorkspaceInternalFileOpenService(
                new ProgressDialogServiceStub(),
                loadService,
                richTextBox,
                session,
                registry,
                new BookmarksService(richTextBox));
        }

        private static IRichTextBoxService CreateRichTextBox() =>
            new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService(),
                new DocumentAppearanceDefaults());

        public void Dispose()
        {
            if(Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
        }

        private sealed class LoadServiceStub: IFlowDocumentLoadService
        {
            public Exception? Exception { get; init; }
            public string Content { get; init; } = string.Empty;

            public Task<FlowDocument> PrepareAsync(
                Stream source,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Exception is null
                    ? Task.FromResult(new FlowDocument(
                        new Paragraph(new Run(Content))))
                    : Task.FromException<FlowDocument>(Exception);
            }

            public async Task LoadAsync(
                IRichTextBoxService richTextBoxService,
                Stream source,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null)
            {
                FlowDocument document = await PrepareAsync(
                    source,
                    template,
                    cancellationToken,
                    progress);
                richTextBoxService.ReplaceDocument(document);
            }
        }

        private sealed class ProgressDialogServiceStub: IProgressDialogService
        {
            public Task<T> RunAsync<T>(
                string operationName,
                Func<IProgressReporter, CancellationToken, Task<T>> operation) =>
                operation(new ProgressReporterStub(), CancellationToken.None);
        }

        private sealed class ProgressReporterStub: IProgressReporter
        {
            public void Report(double? value, string? currentInfo = null)
            {
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

using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Documents;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class DocumentBackupRecoveryServiceTests
    {
        [WpfFact]
        public async Task RestoreAsync_LoadsAdjacentBak_AsDirtyDocument()
        {
            string directory = CreateDirectory();
            string filePath = Path.Combine(directory, "book.txt");
            string backupPath = filePath + ".bak";
            await File.WriteAllTextAsync(filePath, "current");
            await File.WriteAllTextAsync(backupPath, "previous");
            IRichTextBoxService editor = CreateEditor();
            var session = new DocumentSession(editor);
            var template = new PlainTextTemplate();
            session.Open(filePath, template);
            var service = CreateService(
                session,
                editor,
                new TextLoadService(),
                new FileTemplateRegistry([template]));

            try
            {
                bool restored = await service.RestoreAsync();

                Assert.True(restored);
                Assert.Equal(
                    "previous",
                    new TextRange(
                        editor.Document.ContentStart,
                        editor.Document.ContentEnd).Text.TrimEnd('\r', '\n'));
                Assert.True(session.IsDirty);
                Assert.True(File.Exists(backupPath));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [WpfFact]
        public async Task SynchronizeAfterRenameAsync_MovesBakToNewName()
        {
            string directory = CreateDirectory();
            string oldPath = Path.Combine(directory, "old.txt");
            string newPath = Path.Combine(directory, "new.txt");
            await File.WriteAllTextAsync(oldPath + ".bak", "previous");
            IRichTextBoxService editor = CreateEditor();
            var service = CreateService(
                new DocumentSession(editor),
                editor,
                new TextLoadService(),
                new FileTemplateRegistry([new PlainTextTemplate()]));

            try
            {
                await service.SynchronizeAfterRenameAsync(oldPath, newPath);

                Assert.False(File.Exists(oldPath + ".bak"));
                Assert.Equal(
                    "previous",
                    await File.ReadAllTextAsync(newPath + ".bak"));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [WpfFact]
        public async Task SynchronizeAfterEncryptedSaveAsync_RewritesBak_WhenKeyChanged()
        {
            string directory = CreateDirectory();
            string filePath = Path.Combine(directory, "book.cbook");
            await File.WriteAllTextAsync(filePath, "encrypted with new key");
            await File.WriteAllTextAsync(filePath + ".bak", "old key data");
            IRichTextBoxService editor = CreateEditor();
            var service = CreateService(
                new DocumentSession(editor),
                editor,
                new TextLoadService(),
                new FileTemplateRegistry([new SecureFileTemplate()]),
                new ThrowingSecureFileProcessor());

            try
            {
                await service.SynchronizeAfterEncryptedSaveAsync(filePath);

                Assert.Equal(
                    "encrypted with new key",
                    await File.ReadAllTextAsync(filePath + ".bak"));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static DocumentBackupRecoveryService CreateService(
            IDocumentSession session,
            IRichTextBoxService editor,
            IFlowDocumentLoadService loader,
            IFileTemplateRegistry registry,
            ISecureFileProcessor? processor = null) =>
            new(
                session,
                editor,
                loader,
                registry,
                new BookmarksService(editor),
                new PlainFileValidator(),
                processor ?? new ThrowingSecureFileProcessor(),
                new AvailableKeyRequestService());

        private static string CreateDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static IRichTextBoxService CreateEditor() =>
            new RichTextBoxService(
                new TestParagraphFactory(),
                new UriNavigationService(),
                new DocumentAppearanceDefaults());

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

        private sealed class TextLoadService: IFlowDocumentLoadService
        {
            public async Task<FlowDocument> PrepareAsync(
                Stream source,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null)
            {
                using var reader = new StreamReader(
                    source,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    leaveOpen: true);
                string text = await reader.ReadToEndAsync(cancellationToken);
                return new FlowDocument(new Paragraph(new Run(text)));
            }

            public async Task LoadAsync(
                IRichTextBoxService richTextBoxService,
                Stream source,
                IFileTemplate template,
                CancellationToken cancellationToken = default,
                IProgressReporter? progress = null) =>
                richTextBoxService.ReplaceDocument(
                    await PrepareAsync(
                        source,
                        template,
                        cancellationToken,
                        progress));
        }

        private sealed class PlainFileValidator: ISecureFileValidator
        {
            public Task<bool> HasCryptoBookHeaderAsync(
                string filePath,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(false);
        }

        private sealed class AvailableKeyRequestService:
            IEncryptionKeyRequestService
        {
            public bool EnsureKeyAvailable() => true;
        }

        private sealed class ThrowingSecureFileProcessor:
            ISecureFileProcessor
        {
            public Task EncryptFileAsync(
                string inputFile,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task EncryptStreamAsync(
                Stream input,
                string originalExtension,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task DecryptFileAsyncToFile(
                string inputFile,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<DecryptedFileContent> DecryptFileContentAsync(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                Task.FromException<DecryptedFileContent>(
                    new CryptographicException("different key"));

            public Task<Stream> DecryptFileAsyncToStream(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}

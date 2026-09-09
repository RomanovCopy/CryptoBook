using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class FileCreationServiceTests: IDisposable
    {
        private readonly string testDirectory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));

        public FileCreationServiceTests()
        {
            Directory.CreateDirectory(testDirectory);
        }

        [StaFact]
        public async Task SecureTemplate_CreatesEncryptedContainerAndReturnsPath()
        {
            var processor = new SecureFileProcessorStub();
            var secureTemplate = new SecureFileTemplate();
            var registry = new FileTemplateRegistry(
                [secureTemplate, new XamlPackageFileTemplate()]);
            var service = new FileCreationService(
                new LocalFileManagerStub(),
                processor,
                registry);

            FileOperationResult result = await service.CreateAsync(
                testDirectory,
                "secret",
                secureTemplate,
                IfExistsMode.FailIfExists,
                isHidden: false,
                isReadOnly: false,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.AffectedPath);
            string nativePath = result.AffectedPath!["local://".Length..];
            Assert.Equal(
                Path.GetFullPath(Path.Combine(testDirectory, "secret.cbook")),
                Path.GetFullPath(nativePath));
            Assert.True(File.Exists(nativePath));
            Assert.Equal(".XamlPackage", processor.OriginalExtension);
            Assert.NotNull(processor.Plaintext);
            Assert.NotEmpty(processor.Plaintext!);
        }

        public void Dispose()
        {
            if(Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
        }

        [WpfTheory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Create_PreservesSelectedWidthInInitialFile(bool encrypted)
        {
            var processor = new SecureFileProcessorStub();
            IFileTemplate template = encrypted ? new SecureFileTemplate() : new XamlPackageFileTemplate();
            var service = new FileCreationService(new LocalFileManagerStub(), processor,
                new FileTemplateRegistry([new SecureFileTemplate(), new XamlPackageFileTemplate()]));
            var handler = new XamlPackageDocumentFormatHandler(
                new WpfDispatcherService(System.Windows.Threading.Dispatcher.CurrentDispatcher));
            var source = new System.Windows.Documents.FlowDocument(new System.Windows.Documents.Paragraph());
            DocumentPageLayout.Apply(source, DocumentPaperSize.A2, true);
            byte[] content = await handler.SerializeAsync(source);
            FileOperationResult result = await service.CreateAsync(testDirectory, "wide", template,
                IfExistsMode.FailIfExists, false, false, CancellationToken.None, initialContent: content);
            Assert.True(result.Success);
            byte[] saved = encrypted ? processor.Plaintext! :
                await File.ReadAllBytesAsync(result.AffectedPath!["local://".Length..]);
            var restored = new System.Windows.Documents.FlowDocument();
            await handler.LoadAsync(restored, saved);
            Assert.Equal(source.PageWidth, restored.PageWidth);
            Assert.True(double.IsNaN(restored.PageHeight));
        }

        private sealed class SecureFileProcessorStub: ISecureFileProcessor
        {
            public byte[]? Plaintext { get; private set; }
            public string? OriginalExtension { get; private set; }

            public async Task EncryptStreamAsync(
                Stream input,
                string originalExtension,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default)
            {
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, cancellationToken);
                Plaintext = buffer.ToArray();
                OriginalExtension = originalExtension;
                await File.WriteAllBytesAsync(
                    outputFile,
                    [1, 2, 3],
                    cancellationToken);
            }

            public Task EncryptFileAsync(
                string inputFile,
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
                throw new NotSupportedException();

            public Task<Stream> DecryptFileAsyncToStream(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }

        private sealed class LocalFileManagerStub: IFileManagerService
        {
            public string NormalizePath(string rawPath) =>
                rawPath.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                    ? rawPath
                    : "local://" + rawPath;

            public Task<Stream> OpenReadAsync(
                string path,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default)
            {
                string nativePath = Native(path);
                if(!File.Exists(nativePath))
                    throw new FileNotFoundException(null, nativePath);
                return Task.FromResult<Stream>(File.OpenRead(nativePath));
            }

            public Task<Stream> OpenWriteAsync(
                string path,
                bool overwrite,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<Stream>(new FileStream(
                    Native(path),
                    overwrite ? FileMode.Create : FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None));

            public Task<FileOperationResult> SetHiddenAsync(
                string path,
                bool hidden,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(FileOperationResult.Ok());

            public Task<FileOperationResult> SetReadOnlyAsync(
                string path,
                bool isReadOnly,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(FileOperationResult.Ok());

            private static string Native(string path) =>
                path.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                    ? path["local://".Length..]
                    : path;

            public Task<List<ISystemItem>> BrowseAsync(string path, IProgressReporter? progress = null, CancellationToken ct = default, bool includeHidden = false) => throw new NotSupportedException();
            public Task<FileOperationResult> CopyAsync(string sourcePath, string destinationPath, IProgressReporter? progress, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FileOperationResult> MoveAsync(string sourcePath, string destinationPath, IProgressReporter? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FileOperationResult> DeleteAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FileOperationResult> CreateDirectoryAsync(string parentDirectory, string newDirectoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> CanReadAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> IsHiddenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> IsReadOnlyAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }
    }
}

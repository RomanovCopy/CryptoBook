using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;
using System.Windows;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class WorkspaceFileOpenServiceTests: IDisposable
    {
        private readonly string testDirectory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));

        public WorkspaceFileOpenServiceTests()
        {
            Directory.CreateDirectory(testDirectory);
        }

        [Fact]
        public async Task SupportedEncryptedFile_IsOpenedInsideCryptoBook()
        {
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(),
                new SecureFileProcessorStub(".txt"),
                new KeyRequestStub(),
                new WindowManagerStub(),
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener);

            WorkspaceFileOpenResult result = await service.OpenAsync(sourcePath);
            string openedPath = Assert.IsType<string>(internalOpener.DecryptedPath);

            Assert.True(result.Success);
            Assert.True(result.OpenedInternally);
            Assert.NotEqual(sourcePath, openedPath);
            Assert.Equal(".txt", Path.GetExtension(openedPath));
            Assert.Equal(sourcePath, internalOpener.EncryptedPath);
            Assert.IsType<PlainTextTemplate>(internalOpener.ContentTemplate);
            Assert.True(internalOpener.SourceIsEncrypted);
            Assert.Null(launcher.OpenedPath);
            Assert.False(File.Exists(openedPath));

            service.Dispose();
        }

        [Fact]
        public async Task SupportedPlainFile_IsOpenedInsideCryptoBook()
        {
            string sourcePath = Path.Combine(testDirectory, "notes.txt");
            await File.WriteAllTextAsync(sourcePath, "search result");
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(encrypted: false),
                new SecureFileProcessorStub(".txt"),
                new KeyRequestStub(),
                new WindowManagerStub(),
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener);

            WorkspaceFileOpenResult result = await service.OpenAsync(sourcePath);

            Assert.True(result.Success);
            Assert.True(result.OpenedInternally);
            Assert.Equal(sourcePath, internalOpener.SourcePath);
            Assert.Equal(sourcePath, internalOpener.ContentPath);
            Assert.False(internalOpener.SourceIsEncrypted);
            Assert.IsType<PlainTextTemplate>(internalOpener.ContentTemplate);
            Assert.Null(launcher.OpenedPath);

            service.Dispose();
        }

        [Theory]
        [InlineData(".bin")]
        [InlineData(".pdf")]
        public async Task NonDisplayableEncryptedFile_IsOpenedExternally(
            string extension)
        {
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(),
                new SecureFileProcessorStub(extension),
                new KeyRequestStub(),
                new WindowManagerStub(),
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener);

            WorkspaceFileOpenResult result = await service.OpenAsync(sourcePath);
            string openedPath = Assert.IsType<string>(launcher.OpenedPath);

            Assert.True(result.Success);
            Assert.False(result.OpenedInternally);
            Assert.Equal(extension, Path.GetExtension(openedPath));
            Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(openedPath));
            Assert.Null(internalOpener.DecryptedPath);

            service.Dispose();

            Assert.False(File.Exists(openedPath));
        }

        [Fact]
        public async Task EncryptedMedia_IsOpenedInBuiltInViewer()
        {
            string sourcePath = Path.Combine(testDirectory, "image.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var windowManager = new WindowManagerStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(),
                new SecureFileProcessorStub(".png"),
                new KeyRequestStub(),
                windowManager,
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener);

            WorkspaceFileOpenResult result = await service.OpenAsync(sourcePath);
            string playbackPath = Assert.IsType<string>(
                windowManager.CreatedArguments?["path"]);

            Assert.True(result.Success);
            Assert.True(result.OpenedInternally);
            Assert.Equal(typeof(Views.MediaPlayer), windowManager.CreatedWindowType);
            Assert.Equal(1, windowManager.ShowCount);
            Assert.True(File.Exists(playbackPath));
            Assert.Null(launcher.OpenedPath);
            Assert.Null(internalOpener.DecryptedPath);

            service.Dispose();

            Assert.False(File.Exists(playbackPath));
        }

        private static IFileTemplateRegistry CreateTemplateRegistry() =>
            new FileTemplateRegistry(
            [
                new PlainTextTemplate(),
                new SecureFileTemplate(),
                new ImageFileTemplate(),
                new PdfFileTemplate()
            ]);

        public void Dispose()
        {
            if(Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
        }

        private sealed class SecureFileValidatorStub(bool encrypted = true):
            ISecureFileValidator
        {
            public Task<bool> HasCryptoBookHeaderAsync(
                string filePath,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(encrypted);
        }

        private sealed class SecureFileProcessorStub(string extension):
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
                File.WriteAllBytesAsync(
                    outputFile + extension,
                    [4, 5, 6],
                    cancellationToken);

            public Task<Stream> DecryptFileAsyncToStream(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<DecryptedFileContent> DecryptFileContentAsync(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(new DecryptedFileContent(
                    new MemoryStream([4, 5, 6]),
                    extension));
        }

        private sealed class InternalFileOpenServiceStub:
            IWorkspaceInternalFileOpenService
        {
            public string? SourcePath { get; private set; }
            public string? ContentPath { get; private set; }
            public string? EncryptedPath => SourcePath;
            public string? DecryptedPath => ContentPath;
            public IFileTemplate? ContentTemplate { get; private set; }
            public bool SourceIsEncrypted { get; private set; }

            public Task OpenDocumentAsync(
                string sourcePath,
                string contentPath,
                IFileTemplate contentTemplate,
                bool sourceIsEncrypted,
                CancellationToken cancellationToken = default)
            {
                SourcePath = sourcePath;
                ContentPath = contentPath;
                ContentTemplate = contentTemplate;
                SourceIsEncrypted = sourceIsEncrypted;
                return Task.CompletedTask;
            }
        }

        private sealed class KeyRequestStub: IEncryptionKeyRequestService
        {
            public bool EnsureKeyAvailable() => true;
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

        private sealed class FileLauncherStub: IFileLauncherService
        {
            public string? OpenedPath { get; private set; }

            public LaunchResult Open(string target)
            {
                OpenedPath = target;
                return LaunchResult.Ok("open", target);
            }

            public LaunchResult Open(string target, string verb) =>
                throw new NotSupportedException();
            public LaunchResult ShellExecute(ShellLaunchOptions options) =>
                throw new NotSupportedException();
            public LaunchResult OpenWith(
                string applicationPath,
                string target,
                string? arguments = null,
                string? workingDirectory = null) =>
                throw new NotSupportedException();
            public LaunchResult StartProcess(ProcessLaunchOptions options) =>
                throw new NotSupportedException();
            public LaunchResult RevealInExplorer(string path, bool select = true) =>
                throw new NotSupportedException();
            public LaunchResult Print(string path) =>
                throw new NotSupportedException();
            public LaunchResult Edit(string path) =>
                throw new NotSupportedException();
            public LaunchResult RunAsAdmin(
                string path,
                string? arguments = null) =>
                throw new NotSupportedException();
            public LaunchResult RunCmd(
                string command,
                string? workingDirectory = null,
                bool runAsAdmin = false) =>
                throw new NotSupportedException();
            public LaunchResult RunPowerShell(
                string command,
                string? workingDirectory = null,
                bool runAsAdmin = false) =>
                throw new NotSupportedException();
        }

        private sealed class WindowManagerStub: IWindowManager
        {
            public Type? CreatedWindowType { get; private set; }
            public IReadOnlyDictionary<string, object?>? CreatedArguments { get; private set; }
            public int ShowCount { get; private set; }

            public Guid CreateWindow<T>(
                IReadOnlyDictionary<string, object?>? args = null)
                where T: Window
            {
                CreatedWindowType = typeof(T);
                CreatedArguments = args;
                return Guid.NewGuid();
            }
            public TResult? GetResult<TResult>(Guid guid) => default;
            public void ShowWindow(Guid windowId)
            {
                ShowCount++;
            }
            public void ShowWindowDialog(Guid windowId)
            {
            }
            public void ActivateWindow(Guid windowId)
            {
            }
            public void CloseWindow(Guid windowId)
            {
            }
            public bool IsWindowOpen(Guid windowId) => false;
            public WindowHost? FindHostWindow(Guid windowId) => null;
        }
    }
}

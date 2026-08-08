using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;
using System.Text;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class WorkspaceContentSearchServiceTests: IDisposable
    {
        private readonly string testDirectory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.ContentSearchTests",
            Guid.NewGuid().ToString("N"));
        private readonly string indexDirectory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.ContentSearchIndexes",
            Guid.NewGuid().ToString("N"));

        public WorkspaceContentSearchServiceTests()
        {
            Directory.CreateDirectory(testDirectory);
        }

        [Fact]
        public async Task SearchAsync_FindsTextRecursivelyAndBuildsSnippet()
        {
            string nested = Directory.CreateDirectory(
                Path.Combine(testDirectory, "Nested")).FullName;
            await File.WriteAllTextAsync(
                Path.Combine(nested, "notes.txt"),
                "Before\nSecret phrase\tafter and SECRET PHRASE again.");
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "unrelated.txt"),
                "Nothing to find here.");
            WorkspaceContentSearchService service = CreateService(
                new NeverEncryptedValidator(),
                new ProcessorStub(),
                new KeyRequestStub(allowed: false));

            WorkspaceContentSearchOutcome outcome =
                await service.SearchAsync("secret phrase");

            WorkspaceContentSearchResult result =
                Assert.Single(outcome.Results);
            Assert.Equal("notes.txt", result.Name);
            Assert.Equal(
                Path.Combine("Nested", "notes.txt"),
                result.RelativePath);
            Assert.Equal(2, result.MatchCount);
            Assert.Contains("Secret phrase after", result.Snippet);
            Assert.False(result.IsEncrypted);
        }

        [StaFact]
        public async Task SearchAsync_ExtractsVisibleTextFromRtfDocument()
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "formatted.rtf"),
                @"{\rtf1\ansi Visible \b needle\b0  text}",
                Encoding.ASCII);
            var dispatcher = new ImmediateDispatcherService();
            var templateRegistry = new FileTemplateRegistry(
                [new RichTextFileTemplate()]);
            var handlerRegistry = new DocumentFormatHandlerRegistry(
                [new RtfDocumentFormatHandler(dispatcher)]);
            var validator = new NeverEncryptedValidator();
            var options = CreateOptions();
            var index = new WorkspaceSearchIndex(
                validator,
                [new FlowDocumentTextExtractor(
                    handlerRegistry,
                    templateRegistry,
                    dispatcher)],
                options);
            var service = new WorkspaceContentSearchService(
                new WorkspaceStub(testDirectory),
                index,
                new ProcessorStub(),
                new KeyRequestStub(allowed: false));

            WorkspaceContentSearchOutcome outcome =
                await service.SearchAsync("needle");

            WorkspaceContentSearchResult result =
                Assert.Single(outcome.Results);
            Assert.Contains("Visible needle text", result.Snippet);
        }

        [Fact]
        public async Task SearchAsync_RequestsKeyOnceAndSearchesEncryptedStreams()
        {
            string first = Path.Combine(testDirectory, "first.cbook");
            string second = Path.Combine(testDirectory, "second.cbook");
            await File.WriteAllBytesAsync(first, [1, 2, 3]);
            await File.WriteAllBytesAsync(second, [4, 5, 6]);
            var keyRequest = new KeyRequestStub(allowed: true);
            var processor = new ProcessorStub(new Dictionary<string, string>
            {
                [first] = "A hidden needle.",
                [second] = "Another NEEDLE."
            });
            WorkspaceContentSearchService service = CreateService(
                new AlwaysEncryptedValidator(),
                processor,
                keyRequest);

            WorkspaceContentSearchOutcome outcome =
                await service.SearchAsync("needle");

            Assert.Equal(2, outcome.Results.Count);
            Assert.All(outcome.Results, result => Assert.True(result.IsEncrypted));
            Assert.Equal(1, keyRequest.RequestCount);
            Assert.Equal(2, processor.DecryptCount);
            Assert.Equal(0, outcome.SkippedEncryptedFileCount);
        }

        [Fact]
        public async Task SearchAsync_WhenKeyEntryIsCancelled_SkipsEncryptedFiles()
        {
            await File.WriteAllBytesAsync(
                Path.Combine(testDirectory, "first.cbook"),
                [1]);
            await File.WriteAllBytesAsync(
                Path.Combine(testDirectory, "second.cbook"),
                [2]);
            var keyRequest = new KeyRequestStub(allowed: false);
            var processor = new ProcessorStub();
            WorkspaceContentSearchService service = CreateService(
                new AlwaysEncryptedValidator(),
                processor,
                keyRequest);

            WorkspaceContentSearchOutcome outcome =
                await service.SearchAsync("needle");

            Assert.Empty(outcome.Results);
            Assert.Equal(2, outcome.SkippedEncryptedFileCount);
            Assert.Equal(1, keyRequest.RequestCount);
            Assert.Equal(0, processor.DecryptCount);
        }

        [Fact]
        public async Task SearchAsync_ObservesCancellation()
        {
            WorkspaceContentSearchService service = CreateService(
                new NeverEncryptedValidator(),
                new ProcessorStub(),
                new KeyRequestStub(allowed: false));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.SearchAsync(
                    "needle",
                    cancellationToken: cancellation.Token));
        }

        [Fact]
        public async Task SearchAsync_ReusesIndexAndReextractsOnlyChangedFile()
        {
            string first = Path.Combine(testDirectory, "first.txt");
            string second = Path.Combine(testDirectory, "second.txt");
            await File.WriteAllTextAsync(first, "needle one");
            await File.WriteAllTextAsync(second, "needle two");
            var extractor = new CountingTextExtractor();
            var validator = new NeverEncryptedValidator();
            var index = new WorkspaceSearchIndex(
                validator,
                [extractor],
                CreateOptions());
            var service = new WorkspaceContentSearchService(
                new WorkspaceStub(testDirectory),
                index,
                new ProcessorStub(),
                new KeyRequestStub(allowed: false));

            Assert.Equal(2, (await service.SearchAsync("needle")).Results.Count);
            Assert.Equal(2, extractor.ExtractCount);

            Assert.Equal(2, (await service.SearchAsync("needle")).Results.Count);
            Assert.Equal(2, extractor.ExtractCount);

            await File.WriteAllTextAsync(first, "changed needle content");
            Assert.Equal(2, (await service.SearchAsync("needle")).Results.Count);
            Assert.Equal(3, extractor.ExtractCount);

            File.Delete(second);
            Assert.Single((await service.SearchAsync("needle")).Results);
            Assert.Equal(3, extractor.ExtractCount);
        }

        private WorkspaceContentSearchService CreateService(
            ISecureFileValidator validator,
            ISecureFileProcessor processor,
            IEncryptionKeyRequestService keyRequest)
        {
            var index = new WorkspaceSearchIndex(
                validator,
                [new PlainTextDocumentTextExtractor()],
                CreateOptions());
            return new WorkspaceContentSearchService(
                new WorkspaceStub(testDirectory),
                index,
                processor,
                keyRequest);
        }

        private WorkspaceContentSearchOptions CreateOptions() => new()
        {
            MaxResults = 20,
            MaxFileSizeBytes = 1024 * 1024,
            SnippetLength = 80,
            IndexDirectory = indexDirectory
        };

        public void Dispose()
        {
            if(Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
            if(Directory.Exists(indexDirectory))
                Directory.Delete(indexDirectory, recursive: true);
        }

        private sealed class CountingTextExtractor: IDocumentTextExtractor
        {
            private readonly PlainTextDocumentTextExtractor inner = new();
            public int ExtractCount { get; private set; }
            public bool CanExtract(string extension) =>
                inner.CanExtract(extension);
            public async Task<string> ExtractAsync(
                Stream source,
                string extension,
                CancellationToken cancellationToken = default)
            {
                ExtractCount++;
                return await inner.ExtractAsync(
                    source,
                    extension,
                    cancellationToken);
            }
        }

        private sealed class WorkspaceStub(string directory): IWorkspaceService
        {
            public string WorkspaceDirectory => directory;
            public void SetWorkspaceDirectory(string path) =>
                throw new NotSupportedException();
            public Task<WorkspaceSearchOutcome> SearchFilesAsync(
                string query,
                int maxResults = 200,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }

        private sealed class NeverEncryptedValidator: ISecureFileValidator
        {
            public Task<bool> HasCryptoBookHeaderAsync(
                string filePath,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(false);
        }

        private sealed class AlwaysEncryptedValidator: ISecureFileValidator
        {
            public Task<bool> HasCryptoBookHeaderAsync(
                string filePath,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(true);
        }

        private sealed class KeyRequestStub(bool allowed):
            IEncryptionKeyRequestService
        {
            public int RequestCount { get; private set; }

            public bool EnsureKeyAvailable()
            {
                RequestCount++;
                return allowed;
            }
        }

        private sealed class ProcessorStub: ISecureFileProcessor
        {
            private readonly IReadOnlyDictionary<string, string> content;

            public ProcessorStub(
                IReadOnlyDictionary<string, string>? content = null)
            {
                this.content = content ?? new Dictionary<string, string>();
            }

            public int DecryptCount { get; private set; }

            public Task<DecryptedFileContent> DecryptFileContentAsync(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DecryptCount++;
                string text = content.TryGetValue(inputFile, out string? value)
                    ? value
                    : string.Empty;
                return Task.FromResult(new DecryptedFileContent(
                    new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)),
                    ".txt"));
            }

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
            public async Task<Stream> DecryptFileAsyncToStream(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                (await DecryptFileContentAsync(
                    inputFile,
                    progress,
                    cancellationToken)).Content;
        }

        private sealed class ImmediateDispatcherService: IDispatcherService
        {
            public bool CheckAccess() => true;
            public void Invoke(Action action) => action();
            public void BeginInvoke(Action action) => action();
            public Task InvokeAsync(
                Action action,
                DispatcherPriority priority = DispatcherPriority.Background)
            {
                action();
                return Task.CompletedTask;
            }
            public Task<T> InvokeAsync<T>(
                Func<T> func,
                DispatcherPriority priority = DispatcherPriority.Background) =>
                Task.FromResult(func());
        }
    }
}

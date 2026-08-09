using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.ComponentModel;
using System.IO;
using System.Windows.Documents;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class PinnedDocumentTests
    {
        [Fact]
        public async Task JsonStore_RoundTripsVersionedDocumentAtomically()
        {
            string directory = CreateTestDirectory();
            try
            {
                string storePath = Path.Combine(directory, "pinned-documents.json");
                var store = new JsonPinnedDocumentStore(storePath);
                var expected = new[]
                {
                    new PinnedDocument(
                        Path.Combine(directory, "book.cbook"),
                        DateTimeOffset.UtcNow.AddMinutes(-5),
                        DateTimeOffset.UtcNow,
                        0)
                };

                await store.SaveAsync(expected);
                IReadOnlyList<PinnedDocument> actual = await store.LoadAsync();

                Assert.Equal(expected, actual);
                Assert.Contains("\"Version\": 1", await File.ReadAllTextAsync(storePath));
                Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Theory]
        [InlineData("{broken json")]
        [InlineData("{\"Version\":999,\"Items\":[]}")]
        public async Task JsonStore_InvalidOrUnsupportedDocument_ReturnsEmptyList(
            string json)
        {
            string directory = CreateTestDirectory();
            try
            {
                string storePath = Path.Combine(directory, "pinned-documents.json");
                await File.WriteAllTextAsync(storePath, json);

                var store = new JsonPinnedDocumentStore(storePath);

                Assert.Empty(await store.LoadAsync());
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task Service_NormalizesPathsAndDoesNotCreateDuplicates()
        {
            string directory = CreateTestDirectory();
            try
            {
                var store = new MemoryPinnedDocumentStore();
                var service = new PinnedDocumentService(store);
                string path = Path.Combine(directory, "book.rtf");

                await service.PinAsync(path);
                await service.PinAsync(Path.Combine(directory, ".", "book.rtf"));

                PinnedDocument item = Assert.Single(service.Items);
                Assert.Equal(Path.GetFullPath(path), item.Path);
                Assert.Equal(1, store.SaveCount);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task ViewModel_TogglesCurrentPinAndOpensThroughCoordinator()
        {
            string directory = CreateTestDirectory();
            try
            {
                string currentPath = Path.Combine(directory, "current.rtf");
                string targetPath = Path.Combine(directory, "target.rtf");
                await File.WriteAllTextAsync(currentPath, "current");
                await File.WriteAllTextAsync(targetPath, "target");

                var store = new MemoryPinnedDocumentStore();
                var service = new PinnedDocumentService(store);
                var session = new DocumentSessionStub { FilePath = currentPath };
                var coordinator = new DocumentSwitchCoordinatorStub();
                using var viewModel = new PinnedDocumentsViewModel(
                    service,
                    coordinator,
                    session,
                    new MessageServiceStub(),
                    new CurrentDocumentSaverStub(),
                    new FileLauncherService(),
                    new FilePickerServiceStub());
                await viewModel.InitializeAsync();

                await ((AsyncRelayCommand)viewModel.ToggleCurrentCommand)
                    .ExecuteAsync();
                Assert.True(viewModel.IsCurrentDocumentPinned);

                await service.PinAsync(targetPath);
                PinnedDocumentItemViewModel target = Assert.Single(
                    viewModel.Items,
                    item => item.Path == targetPath);
                await ((AsyncRelayCommand)viewModel.OpenCommand)
                    .ExecuteAsync(target);

                Assert.Equal(targetPath, coordinator.LastPath);
                Assert.NotNull(service.Items.Single(item =>
                    item.Path == targetPath).LastOpenedAtUtc);

                await ((AsyncRelayCommand)viewModel.ToggleCurrentCommand)
                    .ExecuteAsync();
                Assert.False(viewModel.IsCurrentDocumentPinned);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task ViewModel_SavesNewDocumentBeforePinning()
        {
            string directory = CreateTestDirectory();
            try
            {
                string savedPath = Path.Combine(directory, "saved.rtf");
                var service = new PinnedDocumentService(
                    new MemoryPinnedDocumentStore());
                var session = new DocumentSessionStub
                {
                    DisplayName = "New document.rtf",
                    HasDocumentOverride = true
                };
                var saver = new CurrentDocumentSaverStub
                {
                    Save = () =>
                    {
                        session.FilePath = savedPath;
                        return Task.FromResult(true);
                    }
                };
                using var viewModel = new PinnedDocumentsViewModel(
                    service,
                    new DocumentSwitchCoordinatorStub(),
                    session,
                    new MessageServiceStub(),
                    saver,
                    new FileLauncherService(),
                    new FilePickerServiceStub());
                await viewModel.InitializeAsync();

                await ((AsyncRelayCommand)viewModel.ToggleCurrentCommand)
                    .ExecuteAsync();

                Assert.Equal(1, saver.SaveCount);
                Assert.Equal(
                    Path.GetFullPath(savedPath),
                    Assert.Single(service.Items).Path);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task ViewModel_CancelledSaveDoesNotPinNewDocument()
        {
            var service = new PinnedDocumentService(
                new MemoryPinnedDocumentStore());
            var session = new DocumentSessionStub
            {
                DisplayName = "New document.rtf",
                HasDocumentOverride = true
            };
            var saver = new CurrentDocumentSaverStub
            {
                Save = () => Task.FromResult(false)
            };
            using var viewModel = new PinnedDocumentsViewModel(
                service,
                new DocumentSwitchCoordinatorStub(),
                session,
                new MessageServiceStub(),
                saver,
                new FileLauncherService(),
                new FilePickerServiceStub());
            await viewModel.InitializeAsync();

            await ((AsyncRelayCommand)viewModel.ToggleCurrentCommand)
                .ExecuteAsync();

            Assert.Equal(1, saver.SaveCount);
            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task Service_UpdatesPathAndPreservesPinnedMetadata()
        {
            string directory = CreateTestDirectory();
            try
            {
                var store = new MemoryPinnedDocumentStore();
                var service = new PinnedDocumentService(store);
                string firstPath = Path.Combine(directory, "first.rtf");
                string secondPath = Path.Combine(directory, "second.rtf");
                string renamedPath = Path.Combine(directory, "renamed.rtf");
                PinnedDocument first = await service.PinAsync(firstPath);
                await service.PinAsync(secondPath);
                await service.MarkOpenedAsync(firstPath);
                PinnedDocument before = service.Items[0];

                await service.UpdatePathAsync(firstPath, renamedPath);

                PinnedDocument renamed = service.Items[0];
                Assert.Equal(Path.GetFullPath(renamedPath), renamed.Path);
                Assert.Equal(before.PinnedAtUtc, renamed.PinnedAtUtc);
                Assert.Equal(before.LastOpenedAtUtc, renamed.LastOpenedAtUtc);
                Assert.Equal(first.SortOrder, renamed.SortOrder);
                Assert.Equal(Path.GetFullPath(secondPath), service.Items[1].Path);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task Service_MovesPinnedDocumentsAndPersistsOrder()
        {
            string directory = CreateTestDirectory();
            try
            {
                var store = new MemoryPinnedDocumentStore();
                var service = new PinnedDocumentService(store);
                string first = Path.Combine(directory, "first.rtf");
                string second = Path.Combine(directory, "second.rtf");
                string third = Path.Combine(directory, "third.rtf");
                await service.PinAsync(first);
                await service.PinAsync(second);
                await service.PinAsync(third);

                await service.MoveAsync(third, -1);
                await service.MoveAsync(first, 1);

                Assert.Equal(
                    [third, first, second],
                    service.Items.Select(item => item.Path));
                Assert.Equal([0, 1, 2], service.Items.Select(item => item.SortOrder));
                Assert.Equal(
                    service.Items,
                    await store.LoadAsync());
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task ViewModel_MissingFileDoesNotSwitchAndCanBeRelocated()
        {
            string directory = CreateTestDirectory();
            try
            {
                string missingPath = Path.Combine(directory, "missing.rtf");
                string replacementPath = Path.Combine(directory, "replacement.rtf");
                await File.WriteAllTextAsync(replacementPath, "replacement");
                var service = new PinnedDocumentService(
                    new MemoryPinnedDocumentStore());
                await service.PinAsync(missingPath);
                var coordinator = new DocumentSwitchCoordinatorStub();
                var messages = new MessageServiceStub();
                using var viewModel = new PinnedDocumentsViewModel(
                    service,
                    coordinator,
                    new DocumentSessionStub(),
                    messages,
                    new CurrentDocumentSaverStub(),
                    new FileLauncherService(),
                    new FilePickerServiceStub
                    {
                        SelectedPath = $"local://{replacementPath}"
                    });
                await viewModel.InitializeAsync();
                PinnedDocumentItemViewModel missing = Assert.Single(
                    viewModel.Items);

                Assert.True(missing.IsMissing);
                await ((AsyncRelayCommand)viewModel.OpenCommand)
                    .ExecuteAsync(missing);
                Assert.Null(coordinator.LastPath);
                Assert.NotNull(messages.LastMessage);

                await ((AsyncRelayCommand)viewModel.RelocateCommand)
                    .ExecuteAsync(missing);

                Assert.Equal(
                    Path.GetFullPath(replacementPath),
                    Assert.Single(service.Items).Path);
                Assert.True(Assert.Single(viewModel.Items).IsAvailable);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string CreateTestDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class MemoryPinnedDocumentStore: IPinnedDocumentStore
        {
            private List<PinnedDocument> items = [];

            public int SaveCount { get; private set; }

            public Task<IReadOnlyList<PinnedDocument>> LoadAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<PinnedDocument>>(items.ToList());

            public Task SaveAsync(
                IReadOnlyCollection<PinnedDocument> documents,
                CancellationToken cancellationToken = default)
            {
                items = documents.ToList();
                SaveCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class DocumentSwitchCoordinatorStub:
            IWorkspaceFileOpenService
        {
            public string? LastPath { get; private set; }

            public Task<WorkspaceFileOpenResult> OpenAsync(
                string targetPath,
                CancellationToken cancellationToken = default)
            {
                LastPath = targetPath;
                return Task.FromResult(WorkspaceFileOpenResult.InternalSuccess());
            }
        }

        private sealed class MessageServiceStub: IMessageService
        {
            public string? LastMessage { get; private set; }

            public Task<Guid> ShowMessage(
                string title,
                string message,
                bool isCanceled = false)
            {
                LastMessage = message;
                return Task.FromResult(Guid.NewGuid());
            }

            public void CloseDialog(Guid id)
            {
            }

            public bool ShowConfirmation(Guid id) => false;
        }

        private sealed class DocumentSessionStub: IDocumentSession
        {
            public string? FilePath { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public IFileTemplate? Template { get; set; }
            public bool IsDirty { get; set; }
            public long Revision { get; set; }
            public long SavedRevision { get; set; }
            public bool HasDocument =>
                HasDocumentOverride ||
                !string.IsNullOrWhiteSpace(FilePath);
            public bool HasDocumentOverride { get; set; }

            public event PropertyChangedEventHandler? PropertyChanged
            {
                add { }
                remove { }
            }

            public void Open(string filePath, IFileTemplate template) =>
                throw new NotSupportedException();

            public void Open(
                string filePath,
                IFileTemplate template,
                FlowDocument document) => throw new NotSupportedException();

            public void Close() => throw new NotSupportedException();
            public void MarkDirty() => throw new NotSupportedException();
            public void MarkSaved(string filePath, IFileTemplate template) =>
                throw new NotSupportedException();
            public void MarkSaved(
                string filePath,
                IFileTemplate template,
                long savedRevision) => throw new NotSupportedException();
            public void Rename(string filePath) => throw new NotSupportedException();
            public void SetDisplayName(string displayName) =>
                throw new NotSupportedException();
        }

        private sealed class CurrentDocumentSaverStub: ICurrentDocumentSaver
        {
            public Func<Task<bool>> Save { get; set; } =
                () => Task.FromResult(true);
            public int SaveCount { get; private set; }

            public Task<bool> TrySaveCurrentAsync(
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SaveCount++;
                return Save();
            }
        }

        private sealed class FilePickerServiceStub: IFilePickerService
        {
            public string? SelectedPath { get; set; }

            public Task<string?> PickFileAsync(
                string? initialDirectory,
                CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(SelectedPath);
            }
        }
    }
}

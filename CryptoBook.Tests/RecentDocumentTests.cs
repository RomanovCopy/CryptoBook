using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class RecentDocumentTests
    {
        [Fact]
        public async Task JsonStore_RoundTripsOnlyVersionedMetadataAtomically()
        {
            string directory = CreateTestDirectory();
            try
            {
                string storePath = Path.Combine(directory, "recent-documents.json");
                var store = new JsonRecentDocumentStore(storePath);
                var expected = new[]
                {
                    new RecentDocument(
                        Path.Combine(directory, "secret.cbook"),
                        DateTimeOffset.UtcNow,
                        3)
                };

                await store.SaveAsync(expected);
                IReadOnlyList<RecentDocument> actual = await store.LoadAsync();
                string json = await File.ReadAllTextAsync(storePath);

                Assert.Equal(expected, actual);
                Assert.Contains("\"Version\": 1", json);
                Assert.DoesNotContain("Content", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Key", json, StringComparison.OrdinalIgnoreCase);
                Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
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
                string storePath = Path.Combine(directory, "recent-documents.json");
                await File.WriteAllTextAsync(storePath, json);

                var store = new JsonRecentDocumentStore(storePath);

                Assert.Empty(await store.LoadAsync());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Service_RecordsOpenAndSaveWithoutDuplicates()
        {
            string directory = CreateTestDirectory();
            try
            {
                var store = new MemoryRecentDocumentStore();
                var service = new RecentDocumentService(store);
                string path = Path.Combine(directory, "notes.rtf");

                await service.RecordOpenedAsync(path);
                DateTimeOffset openedAt = Assert.Single(service.Items)
                    .LastAccessedAtUtc;
                await service.RecordOpenedAsync(
                    Path.Combine(directory, ".", "notes.rtf"));
                await service.RecordSavedAsync(path);

                RecentDocument document = Assert.Single(service.Items);
                Assert.Equal(Path.GetFullPath(path), document.Path);
                Assert.Equal(2, document.OpenCount);
                Assert.True(document.LastAccessedAtUtc >= openedAt);
                Assert.Equal(3, store.SaveCount);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Service_CapsHistoryAndSupportsRelocateAndRemove()
        {
            string directory = CreateTestDirectory();
            try
            {
                var store = new MemoryRecentDocumentStore();
                var service = new RecentDocumentService(store, capacity: 2);
                string first = Path.Combine(directory, "first.txt");
                string second = Path.Combine(directory, "second.txt");
                string third = Path.Combine(directory, "third.txt");
                string relocated = Path.Combine(directory, "relocated.txt");

                await service.RecordOpenedAsync(first);
                await service.RecordOpenedAsync(second);
                await service.RecordSavedAsync(third);

                Assert.Equal(
                    [Path.GetFullPath(third), Path.GetFullPath(second)],
                    service.Items.Select(item => item.Path));

                await service.UpdatePathAsync(second, relocated);
                Assert.Contains(
                    service.Items,
                    item => item.Path == Path.GetFullPath(relocated));

                await service.RemoveAsync(third);
                Assert.Equal(
                    Path.GetFullPath(relocated),
                    Assert.Single(service.Items).Path);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void ItemViewModel_ReportsMovedOrUnavailableFile()
        {
            string missingPath = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"),
                "missing.cbook");
            var item = new RecentDocumentItemViewModel(
                new RecentDocument(missingPath, DateTimeOffset.UtcNow, 1));

            Assert.True(item.IsMissing);
            Assert.False(item.IsAvailable);
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

        private sealed class MemoryRecentDocumentStore: IRecentDocumentStore
        {
            private List<RecentDocument> items = [];

            public int SaveCount { get; private set; }

            public Task<IReadOnlyList<RecentDocument>> LoadAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<RecentDocument>>(items.ToList());

            public Task SaveAsync(
                IReadOnlyCollection<RecentDocument> documents,
                CancellationToken cancellationToken = default)
            {
                items = documents.ToList();
                SaveCount++;
                return Task.CompletedTask;
            }
        }
    }
}

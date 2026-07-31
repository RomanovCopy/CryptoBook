using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using Xunit;

namespace CryptoBook.Tests
{
    public sealed class FavoriteDirectoryTests
    {
        [Fact]
        public async Task JsonStore_RoundTripsFavorites()
        {
            string directory = CreateTestDirectory();
            try
            {
                string filePath = Path.Combine(directory, "favorites.json");
                var store = new JsonFavoriteDirectoryStore(filePath);
                var expected = new[]
                {
                    new FavoriteDirectory(Guid.NewGuid(), "local://C:\\Docs", "Документы", 0)
                };

                await store.SaveAsync(expected);
                var actual = await store.LoadAsync();

                Assert.Equal(expected, actual);
                Assert.Contains("\"Version\": 1", await File.ReadAllTextAsync(filePath));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task Service_DoesNotAddNormalizedDuplicate()
        {
            var store = new MemoryFavoriteStore();
            var service = new FavoriteDirectoryService(store, new TestPathPolicy());

            await service.AddAsync(@"C:\Docs\");
            await service.AddAsync(@"c:\docs");

            Assert.Single(service.Items);
            Assert.Equal(1, store.SaveCount);
        }

        [Fact]
        public async Task Service_RenamesAndRemovesFavorite()
        {
            var store = new MemoryFavoriteStore();
            var service = new FavoriteDirectoryService(store, new TestPathPolicy());
            var favorite = await service.AddAsync(@"C:\Docs");

            await service.RenameAsync(favorite.Id, "  Работа  ");
            Assert.Equal("Работа", Assert.Single(service.Items).DisplayName);

            await service.RemoveAsync(favorite.Id);
            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task JsonStore_ReturnsEmptyListForMalformedDocument()
        {
            string directory = CreateTestDirectory();
            try
            {
                string filePath = Path.Combine(directory, "favorites.json");
                await File.WriteAllTextAsync(filePath, "{broken json");
                var store = new JsonFavoriteDirectoryStore(filePath);

                var actual = await store.LoadAsync();

                Assert.Empty(actual);
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

        private sealed class MemoryFavoriteStore: IFavoriteDirectoryStore
        {
            private List<FavoriteDirectory> _items = [];
            public int SaveCount { get; private set; }

            public Task<IReadOnlyList<FavoriteDirectory>> LoadAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<FavoriteDirectory>>(_items.ToList());
            }

            public Task SaveAsync(
                IReadOnlyCollection<FavoriteDirectory> favorites,
                CancellationToken cancellationToken = default)
            {
                _items = favorites.ToList();
                SaveCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class TestPathPolicy: IFavoriteDirectoryPathPolicy
        {
            public string Normalize(string path) =>
                "local://" + path.Trim().TrimEnd('\\', '/').ToUpperInvariant();

            public string GetDefaultDisplayName(string normalizedPath) =>
                Path.GetFileName(GetDisplayPath(normalizedPath)) is { Length: > 0 } name
                    ? name
                    : GetDisplayPath(normalizedPath);

            public string GetDisplayPath(string normalizedPath) =>
                normalizedPath["local://".Length..];

            public Task<bool> IsAvailableAsync(
                string normalizedPath,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(true);
        }
    }
}

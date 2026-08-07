using CryptoBook.DTO;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class JsonUpdateCheckStateStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsState()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "update-check.json");
            var store = new JsonUpdateCheckStateStore(path);
            var expected = new UpdateCheckState(
                new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero),
                "1.2.0");

            await store.SaveAsync(expected);
            UpdateCheckState actual = await store.LoadAsync();

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsEmptyStateForInvalidJson()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "update-check.json");
            await File.WriteAllTextAsync(path, "not-json");
            var store = new JsonUpdateCheckStateStore(path);

            UpdateCheckState state = await store.LoadAsync();

            Assert.Equal(UpdateCheckState.Empty, state);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"CryptoBook.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}

using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class BatchRenameDocumentLinksTests
{
    [Fact]
    public async Task SwapsAndCaseOnlyChangesPreserveIndividualMetadata()
    {
        using var directory = new BatchRenameTests.TestDirectory();
        string a = Path.Combine(directory.Path, "a.txt"), b = Path.Combine(directory.Path, "b.txt");
        var pinnedStore = new JsonPinnedDocumentStore(Path.Combine(directory.Path, "pinned.json"));
        var recentStore = new JsonRecentDocumentStore(Path.Combine(directory.Path, "recent.json"));
        var pinned = new PinnedDocumentService(pinnedStore);
        var recent = new RecentDocumentService(recentStore);
        await pinned.PinAsync(a);
        await pinned.PinAsync(b);
        await recent.RecordOpenedAsync(a);
        await recent.RecordOpenedAsync(a);
        await recent.RecordOpenedAsync(b);
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [a] = b, [b] = a };
        await pinned.UpdatePathsAsync(mapping);
        await recent.UpdatePathsAsync(mapping);
        Assert.Equal(new[] { b, a }, pinned.Items.Select(i => i.Path));
        Assert.Equal(2, recent.Items.Single(i => i.Path == b).OpenCount);
        Assert.Equal(1, recent.Items.Single(i => i.Path == a).OpenCount);
        string upper = Path.Combine(directory.Path, "A.TXT");
        await pinned.UpdatePathsAsync(new Dictionary<string, string> { [a] = upper });
        await recent.UpdatePathsAsync(new Dictionary<string, string> { [a] = upper });
        Assert.Contains(pinned.Items, i => i.Path == upper);
        Assert.Contains(recent.Items, i => i.Path == upper);
        Assert.Equal(pinned.Items, await pinnedStore.LoadAsync());
        Assert.Equal(recent.Items, await recentStore.LoadAsync());
    }

    [Fact]
    public async Task FailedSaveRestoresMetadataSnapshot()
    {
        string a = @"C:\files\a.txt", b = @"C:\files\b.txt";
        var pinned = new PinnedDocumentService(new FailingPinnedStore(a));
        var recent = new RecentDocumentService(new FailingRecentStore(a));
        var mapping = new Dictionary<string, string> { [a] = b };
        await Assert.ThrowsAsync<IOException>(() => pinned.UpdatePathsAsync(mapping));
        await Assert.ThrowsAsync<IOException>(() => recent.UpdatePathsAsync(mapping));
        Assert.Equal(a, Assert.Single(pinned.Items).Path);
        Assert.Equal(a, Assert.Single(recent.Items).Path);
    }

    private sealed class FailingPinnedStore(string path): IPinnedDocumentStore
    {
        public Task<IReadOnlyList<PinnedDocument>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PinnedDocument>>([new(path, DateTimeOffset.UtcNow, null, 0)]);
        public Task SaveAsync(IReadOnlyCollection<PinnedDocument> items, CancellationToken cancellationToken = default) =>
            throw new IOException("Test write failure");
    }

    private sealed class FailingRecentStore(string path): IRecentDocumentStore
    {
        public Task<IReadOnlyList<RecentDocument>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RecentDocument>>([new(path, DateTimeOffset.UtcNow, 2)]);
        public Task SaveAsync(IReadOnlyCollection<RecentDocument> items, CancellationToken cancellationToken = default) =>
            throw new IOException("Test write failure");
    }
}

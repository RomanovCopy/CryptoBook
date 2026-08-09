using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class RecentDocumentService: IRecentDocumentService
    {
        private const int DefaultCapacity = 20;
        private readonly IRecentDocumentStore store;
        private readonly int capacity;
        private readonly SemaphoreSlim gate = new(1, 1);
        private List<RecentDocument> items = [];
        private bool initialized;

        public RecentDocumentService(IRecentDocumentStore store)
            : this(store, DefaultCapacity)
        {
        }

        public RecentDocumentService(IRecentDocumentStore store, int capacity)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.capacity = capacity > 0
                ? capacity
                : throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        public event EventHandler? Changed;

        public IReadOnlyList<RecentDocument> Items => items;

        public async Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            bool changed = false;
            await gate.WaitAsync(cancellationToken);
            try
            {
                if(initialized)
                    return;

                var uniquePaths = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                items = (await store.LoadAsync(cancellationToken))
                    .Select(TryNormalize)
                    .Where(item => item is not null)
                    .Select(item => item!)
                    .OrderByDescending(item => item.LastAccessedAtUtc)
                    .Where(item => uniquePaths.Add(item.Path))
                    .Take(capacity)
                    .ToList();
                initialized = true;
                changed = true;
            }
            finally
            {
                gate.Release();
            }

            if(changed)
                Changed?.Invoke(this, EventArgs.Empty);
        }

        public Task RecordOpenedAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            RecordAsync(path, incrementOpenCount: true, cancellationToken);

        public Task RecordSavedAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            RecordAsync(path, incrementOpenCount: false, cancellationToken);

        private async Task RecordAsync(
            string path,
            bool incrementOpenCount,
            CancellationToken cancellationToken)
        {
            await InitializeAsync(cancellationToken);
            string normalizedPath = Normalize(path);
            List<RecentDocument> previous;

            await gate.WaitAsync(cancellationToken);
            try
            {
                previous = items.ToList();
                RecentDocument? existing = items.FirstOrDefault(item =>
                    PathEquals(item.Path, normalizedPath));
                int openCount = existing?.OpenCount ?? 0;
                if(incrementOpenCount)
                    openCount++;

                items = items
                    .Where(item => !PathEquals(item.Path, normalizedPath))
                    .Prepend(new RecentDocument(
                        normalizedPath,
                        DateTimeOffset.UtcNow,
                        openCount))
                    .Take(capacity)
                    .ToList();

                try
                {
                    await store.SaveAsync(items, cancellationToken);
                }
                catch
                {
                    items = previous;
                    throw;
                }
            }
            finally
            {
                gate.Release();
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public async Task UpdatePathAsync(
            string oldPath,
            string newPath,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);
            string normalizedOldPath = Normalize(oldPath);
            string normalizedNewPath = Normalize(newPath);
            List<RecentDocument>? previous = null;

            await gate.WaitAsync(cancellationToken);
            try
            {
                RecentDocument? source = items.FirstOrDefault(item =>
                    PathEquals(item.Path, normalizedOldPath));
                if(source is null || PathEquals(normalizedOldPath, normalizedNewPath))
                    return;

                previous = items.ToList();
                RecentDocument? target = items.FirstOrDefault(item =>
                    PathEquals(item.Path, normalizedNewPath));
                var merged = source with
                {
                    Path = normalizedNewPath,
                    LastAccessedAtUtc = target is null
                        ? source.LastAccessedAtUtc
                        : source.LastAccessedAtUtc > target.LastAccessedAtUtc
                            ? source.LastAccessedAtUtc
                            : target.LastAccessedAtUtc,
                    OpenCount = source.OpenCount + (target?.OpenCount ?? 0)
                };
                items = items
                    .Where(item =>
                        !PathEquals(item.Path, normalizedOldPath) &&
                        !PathEquals(item.Path, normalizedNewPath))
                    .Append(merged)
                    .OrderByDescending(item => item.LastAccessedAtUtc)
                    .Take(capacity)
                    .ToList();
                await store.SaveAsync(items, cancellationToken);
            }
            catch
            {
                if(previous is not null)
                    items = previous;
                throw;
            }
            finally
            {
                gate.Release();
            }

            if(previous is not null)
                Changed?.Invoke(this, EventArgs.Empty);
        }

        public async Task RemoveAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);
            string normalizedPath = Normalize(path);
            List<RecentDocument>? previous = null;

            await gate.WaitAsync(cancellationToken);
            try
            {
                if(!items.Any(item => PathEquals(item.Path, normalizedPath)))
                    return;

                previous = items.ToList();
                items = items
                    .Where(item => !PathEquals(item.Path, normalizedPath))
                    .ToList();
                await store.SaveAsync(items, cancellationToken);
            }
            catch
            {
                if(previous is not null)
                    items = previous;
                throw;
            }
            finally
            {
                gate.Release();
            }

            if(previous is not null)
                Changed?.Invoke(this, EventArgs.Empty);
        }

        private static RecentDocument? TryNormalize(RecentDocument item)
        {
            try
            {
                return string.IsNullOrWhiteSpace(item.Path)
                    ? null
                    : item with
                    {
                        Path = Normalize(item.Path),
                        OpenCount = Math.Max(0, item.OpenCount)
                    };
            }
            catch(Exception exception) when(
                exception is ArgumentException or NotSupportedException)
            {
                return null;
            }
        }

        private static string Normalize(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return Path.GetFullPath(path.Trim());
        }

        private static bool PathEquals(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

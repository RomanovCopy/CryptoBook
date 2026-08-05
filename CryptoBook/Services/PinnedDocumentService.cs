using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services
{
    /// <summary>
    /// Нормализует пути и предоставляет согласованную коллекцию закреплений.
    /// </summary>
    public sealed class PinnedDocumentService: IPinnedDocumentService
    {
        private readonly IPinnedDocumentStore store;
        private readonly SemaphoreSlim gate = new(1, 1);
        private List<PinnedDocument> items = [];
        private bool initialized;

        public PinnedDocumentService(IPinnedDocumentStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public event EventHandler? Changed;

        public IReadOnlyList<PinnedDocument> Items => items;

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
                    .Where(item => uniquePaths.Add(item.Path))
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.PinnedAtUtc)
                    .Select((item, index) => item with { SortOrder = index })
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

        public async Task<PinnedDocument> PinAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);
            string normalizedPath = Normalize(path);
            PinnedDocument? added = null;
            PinnedDocument result;

            await gate.WaitAsync(cancellationToken);
            try
            {
                PinnedDocument? existing = Find(normalizedPath);
                if(existing is not null)
                    return existing;

                added = new PinnedDocument(
                    normalizedPath,
                    DateTimeOffset.UtcNow,
                    null,
                    items.Count);
                items.Add(added);
                await store.SaveAsync(items, cancellationToken);
                result = added;
            }
            catch
            {
                if(added is not null)
                    items.Remove(added);
                throw;
            }
            finally
            {
                gate.Release();
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public async Task UnpinAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);
            string normalizedPath = Normalize(path);
            List<PinnedDocument>? previous = null;

            await gate.WaitAsync(cancellationToken);
            try
            {
                if(Find(normalizedPath) is null)
                    return;

                previous = items;
                items = items
                    .Where(item => !PathEquals(item.Path, normalizedPath))
                    .Select((item, index) => item with { SortOrder = index })
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

        public async Task MarkOpenedAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);
            string normalizedPath = Normalize(path);
            List<PinnedDocument>? previous = null;

            await gate.WaitAsync(cancellationToken);
            try
            {
                int index = items.FindIndex(item =>
                    PathEquals(item.Path, normalizedPath));
                if(index < 0)
                    return;

                previous = items.ToList();
                items[index] = items[index] with
                {
                    LastOpenedAtUtc = DateTimeOffset.UtcNow
                };
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

        public async Task UpdatePathAsync(
            string oldPath,
            string newPath,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);
            string normalizedOldPath = Normalize(oldPath);
            string normalizedNewPath = Normalize(newPath);
            List<PinnedDocument>? previous = null;

            await gate.WaitAsync(cancellationToken);
            try
            {
                PinnedDocument? source = Find(normalizedOldPath);
                if(source is null || PathEquals(normalizedOldPath, normalizedNewPath))
                    return;

                previous = items.ToList();
                List<PinnedDocument> updated = items
                    .Where(item =>
                        !PathEquals(item.Path, normalizedOldPath) &&
                        !PathEquals(item.Path, normalizedNewPath))
                    .OrderBy(item => item.SortOrder)
                    .ToList();
                int insertIndex = Math.Clamp(source.SortOrder, 0, updated.Count);
                updated.Insert(
                    insertIndex,
                    source with { Path = normalizedNewPath });
                items = updated
                    .Select((item, index) => item with { SortOrder = index })
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

        public async Task MoveAsync(
            string path,
            int offset,
            CancellationToken cancellationToken = default)
        {
            if(offset == 0)
                return;

            await InitializeAsync(cancellationToken);
            string normalizedPath = Normalize(path);
            List<PinnedDocument>? previous = null;

            await gate.WaitAsync(cancellationToken);
            try
            {
                int oldIndex = items.FindIndex(item =>
                    PathEquals(item.Path, normalizedPath));
                if(oldIndex < 0)
                    return;

                int newIndex = Math.Clamp(oldIndex + offset, 0, items.Count - 1);
                if(oldIndex == newIndex)
                    return;

                previous = items.ToList();
                PinnedDocument moved = items[oldIndex];
                items.RemoveAt(oldIndex);
                items.Insert(newIndex, moved);
                items = items
                    .Select((item, index) => item with { SortOrder = index })
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

        public bool IsPinned(string path)
        {
            if(string.IsNullOrWhiteSpace(path))
                return false;

            string normalizedPath;
            try
            {
                normalizedPath = Normalize(path);
            }
            catch(ArgumentException)
            {
                return false;
            }
            catch(NotSupportedException)
            {
                return false;
            }

            return Find(normalizedPath) is not null;
        }

        private PinnedDocument? Find(string normalizedPath) =>
            items.FirstOrDefault(item => PathEquals(item.Path, normalizedPath));

        private static PinnedDocument? TryNormalize(PinnedDocument item)
        {
            try
            {
                return string.IsNullOrWhiteSpace(item.Path)
                    ? null
                    : item with { Path = Normalize(item.Path) };
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

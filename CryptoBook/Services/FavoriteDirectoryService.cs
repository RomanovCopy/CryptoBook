using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class FavoriteDirectoryService: IFavoriteDirectoryService
    {
        private readonly IFavoriteDirectoryStore _store;
        private readonly IFavoriteDirectoryPathPolicy _pathPolicy;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private List<FavoriteDirectory> _items = [];
        private bool _initialized;

        public FavoriteDirectoryService(
            IFavoriteDirectoryStore store,
            IFavoriteDirectoryPathPolicy pathPolicy)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _pathPolicy = pathPolicy ?? throw new ArgumentNullException(nameof(pathPolicy));
        }

        public event EventHandler? Changed;
        public IReadOnlyList<FavoriteDirectory> Items => _items;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if(_initialized)
                    return;

                var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _items = (await _store.LoadAsync(cancellationToken))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                    .Select(item => item with { Path = _pathPolicy.Normalize(item.Path) })
                    .Where(item => uniquePaths.Add(item.Path))
                    .OrderBy(item => item.SortOrder)
                    .Select((item, index) => item with
                    {
                        DisplayName = string.IsNullOrWhiteSpace(item.DisplayName)
                            ? _pathPolicy.GetDefaultDisplayName(item.Path)
                            : item.DisplayName.Trim(),
                        SortOrder = index
                    })
                    .ToList();
                _initialized = true;
            }
            finally
            {
                _gate.Release();
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public async Task<FavoriteDirectory> AddAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);
            string normalizedPath = _pathPolicy.Normalize(path);

            await _gate.WaitAsync(cancellationToken);
            FavoriteDirectory favorite;
            try
            {
                var existing = _items.FirstOrDefault(item =>
                    string.Equals(item.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
                if(existing is not null)
                    return existing;

                favorite = new FavoriteDirectory(
                    Guid.NewGuid(),
                    normalizedPath,
                    _pathPolicy.GetDefaultDisplayName(normalizedPath),
                    _items.Count);
                _items.Add(favorite);
                await _store.SaveAsync(_items, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return favorite;
        }

        public async Task RenameAsync(
            Guid id,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            string normalizedName = displayName?.Trim() ?? string.Empty;
            if(normalizedName.Length == 0)
                throw new ArgumentException("Имя закладки не может быть пустым.", nameof(displayName));

            await InitializeAsync(cancellationToken);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                int index = _items.FindIndex(item => item.Id == id);
                if(index < 0)
                    return;

                _items[index] = _items[index] with { DisplayName = normalizedName };
                await _store.SaveAsync(_items, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);
            await _gate.WaitAsync(cancellationToken);
            bool removed;
            try
            {
                removed = _items.RemoveAll(item => item.Id == id) > 0;
                if(!removed)
                    return;

                _items = _items
                    .Select((item, index) => item with { SortOrder = index })
                    .ToList();
                await _store.SaveAsync(_items, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public Task<bool> IsAvailableAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return _pathPolicy.IsAvailableAsync(path, cancellationToken);
        }

        public string GetDisplayPath(string path) => _pathPolicy.GetDisplayPath(path);
    }
}

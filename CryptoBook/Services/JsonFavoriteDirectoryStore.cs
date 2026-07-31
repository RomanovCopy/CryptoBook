using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Text.Json;

namespace CryptoBook.Services
{
    public sealed class JsonFavoriteDirectoryStore: IFavoriteDirectoryStore
    {
        private const int CurrentVersion = 1;
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public JsonFavoriteDirectoryStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CryptoBook",
                "favorites.json"))
        {
        }

        public JsonFavoriteDirectoryStore(string filePath)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath)
                ? throw new ArgumentException("Путь к хранилищу не задан.", nameof(filePath))
                : filePath;
        }

        public async Task<IReadOnlyList<FavoriteDirectory>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            if(!File.Exists(_filePath))
                return [];

            try
            {
                await using var stream = new FileStream(
                    _filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var document = await JsonSerializer.DeserializeAsync<FavoriteDocument>(
                    stream,
                    _jsonOptions,
                    cancellationToken);
                return document?.Version == CurrentVersion
                    ? document.Items ?? []
                    : [];
            }
            catch(JsonException)
            {
                return [];
            }
            catch(IOException)
            {
                return [];
            }
        }

        public async Task SaveAsync(
            IReadOnlyCollection<FavoriteDirectory> favorites,
            CancellationToken cancellationToken = default)
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if(string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Не удалось определить каталог хранилища.");

            Directory.CreateDirectory(directory);
            string temporaryPath = _filePath + ".tmp";
            try
            {
                await using(var stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        new FavoriteDocument(CurrentVersion, favorites.ToList()),
                        _jsonOptions,
                        cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                File.Move(temporaryPath, _filePath, true);
            }
            finally
            {
                if(File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private sealed record FavoriteDocument(
            int Version,
            List<FavoriteDirectory> Items);
    }
}

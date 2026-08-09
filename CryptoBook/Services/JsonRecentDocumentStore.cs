using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Text.Json;

namespace CryptoBook.Services
{
    /// <summary>
    /// Версионируемое атомарное хранилище истории. В JSON находятся только
    /// пути и статистика использования, но не содержимое документов или ключи.
    /// </summary>
    public sealed class JsonRecentDocumentStore: IRecentDocumentStore
    {
        private const int CurrentVersion = 1;
        private readonly string filePath;
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true
        };

        public JsonRecentDocumentStore()
            : this(Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "CryptoBook",
                "recent-documents.json"))
        {
        }

        public JsonRecentDocumentStore(string filePath)
        {
            this.filePath = string.IsNullOrWhiteSpace(filePath)
                ? throw new ArgumentException(
                    "Путь к хранилищу истории не задан.",
                    nameof(filePath))
                : Path.GetFullPath(filePath);
        }

        public async Task<IReadOnlyList<RecentDocument>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            if(!File.Exists(filePath))
                return [];

            try
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                RecentDocumentEnvelope? envelope =
                    await JsonSerializer.DeserializeAsync<RecentDocumentEnvelope>(
                        stream,
                        jsonOptions,
                        cancellationToken);

                return envelope?.Version == CurrentVersion
                    ? envelope.Items ?? []
                    : [];
            }
            catch(JsonException)
            {
                return [];
            }
        }

        public async Task SaveAsync(
            IReadOnlyCollection<RecentDocument> documents,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(documents);

            string? directory = Path.GetDirectoryName(filePath);
            if(string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException(
                    "Не удалось определить каталог хранилища истории.");

            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await using(var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        new RecentDocumentEnvelope(
                            CurrentVersion,
                            documents
                                .OrderByDescending(item => item.LastAccessedAtUtc)
                                .ToList()),
                        jsonOptions,
                        cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(flushToDisk: true);
                }

                AtomicFileCommit.CommitWithoutBackup(temporaryPath, filePath);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if(File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Сбой очистки временного файла не отменяет опубликованную запись.
            }
        }

        private sealed record RecentDocumentEnvelope(
            int Version,
            List<RecentDocument> Items);
    }
}

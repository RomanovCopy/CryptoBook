using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Text.Json;

namespace CryptoBook.Services
{
    /// <summary>
    /// Версионируемое JSON-хранилище Quick Access. Новый документ сначала
    /// полностью записывается во временный файл, затем атомарно публикуется.
    /// </summary>
    public sealed class JsonPinnedDocumentStore: IPinnedDocumentStore
    {
        private const int CurrentVersion = 1;
        private readonly string filePath;
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true
        };

        public JsonPinnedDocumentStore()
            : this(System.IO.Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "CryptoBook",
                "pinned-documents.json"))
        {
        }

        public JsonPinnedDocumentStore(string filePath)
        {
            this.filePath = string.IsNullOrWhiteSpace(filePath)
                ? throw new ArgumentException(
                    "Путь к хранилищу закреплённых документов не задан.",
                    nameof(filePath))
                : System.IO.Path.GetFullPath(filePath);
        }

        public async Task<IReadOnlyList<PinnedDocument>> LoadAsync(
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
                PinnedDocumentEnvelope? envelope =
                    await JsonSerializer.DeserializeAsync<PinnedDocumentEnvelope>(
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
            IReadOnlyCollection<PinnedDocument> documents,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(documents);

            string? directory = Path.GetDirectoryName(filePath);
            if(string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    "Не удалось определить каталог хранилища закреплений.");
            }

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
                        new PinnedDocumentEnvelope(
                            CurrentVersion,
                            documents.OrderBy(item => item.SortOrder).ToList()),
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
                // Сбой очистки временного файла не отменяет уже завершённую запись.
            }
        }

        private sealed record PinnedDocumentEnvelope(
            int Version,
            List<PinnedDocument> Items);
    }
}

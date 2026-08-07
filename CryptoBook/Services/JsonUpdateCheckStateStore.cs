using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Text.Json;

namespace CryptoBook.Services
{
    public sealed class JsonUpdateCheckStateStore: IUpdateCheckStateStore
    {
        private readonly string filePath;

        public JsonUpdateCheckStateStore()
            : this(Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "CryptoBook",
                "update-check.json"))
        {
        }

        public JsonUpdateCheckStateStore(string filePath)
        {
            this.filePath = string.IsNullOrWhiteSpace(filePath)
                ? throw new ArgumentException(
                    "Update state path is required.",
                    nameof(filePath))
                : Path.GetFullPath(filePath);
        }

        public async Task<UpdateCheckState> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            if(!File.Exists(filePath))
                return UpdateCheckState.Empty;

            try
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                return await JsonSerializer.DeserializeAsync<UpdateCheckState>(
                    stream,
                    cancellationToken: cancellationToken) ?? UpdateCheckState.Empty;
            }
            catch(JsonException)
            {
                return UpdateCheckState.Empty;
            }
            catch(IOException)
            {
                return UpdateCheckState.Empty;
            }
            catch(UnauthorizedAccessException)
            {
                return UpdateCheckState.Empty;
            }
        }

        public async Task SaveAsync(
            UpdateCheckState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            string? directory = Path.GetDirectoryName(filePath);
            if(string.IsNullOrWhiteSpace(directory))
                return;

            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                Directory.CreateDirectory(directory);
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
                        state,
                        cancellationToken: cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(flushToDisk: true);
                }

                AtomicFileCommit.CommitWithoutBackup(temporaryPath, filePath);
            }
            catch(IOException)
            {
                // Отсутствие доступа к необязательному кэшу не мешает работе приложения.
            }
            catch(UnauthorizedAccessException)
            {
                // Отсутствие доступа к необязательному кэшу не мешает работе приложения.
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
            catch(IOException)
            {
            }
            catch(UnauthorizedAccessException)
            {
            }
        }
    }
}

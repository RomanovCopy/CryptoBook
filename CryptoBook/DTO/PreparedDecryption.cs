using System.IO;

namespace CryptoBook.DTO
{
    public sealed class PreparedDecryption: IDisposable, IAsyncDisposable
    {
        private bool disposed;

        internal PreparedDecryption(
            string sourcePath,
            string contentPath,
            string originalExtension,
            string temporaryDirectory)
        {
            SourcePath = Path.GetFullPath(sourcePath);
            ContentPath = Path.GetFullPath(contentPath);
            OriginalExtension = originalExtension;
            TemporaryDirectory = Path.GetFullPath(temporaryDirectory);
        }

        public string SourcePath { get; }
        public string ContentPath { get; }
        public string OriginalExtension { get; }
        public string TemporaryDirectory { get; }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            TryDeleteTemporaryDirectory();
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private void TryDeleteTemporaryDirectory()
        {
            try
            {
                if(Directory.Exists(TemporaryDirectory))
                    Directory.Delete(TemporaryDirectory, recursive: true);
            }
            catch(IOException)
            {
                // Очистка будет повторена при следующем запуске приложения.
            }
            catch(UnauthorizedAccessException)
            {
            }
        }
    }
}

using CryptoBook.Interfaces;

using CryptoBook.Infrastructure;

using System.IO;
using System.Windows.Media.Imaging;

namespace CryptoBook.Services
{
    public sealed class ImageContentLoader: IImageContentLoader
    {
        private readonly IFileManagerService fileManager;

        public ImageContentLoader(IFileManagerService fileManager)
        {
            this.fileManager = fileManager
                ?? throw new ArgumentNullException(nameof(fileManager));
        }

        public async Task<BitmapSource> LoadFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            await using Stream stream = await fileManager.OpenReadAsync(
                filePath,
                cancellationToken: cancellationToken);
            return await LoadAsync(stream, cancellationToken);
        }

        public async Task<BitmapSource> LoadAsync(
            Stream source,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);

            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            byte[] bytes = buffer.ToArray();
            if(bytes.Length == 0)
                throw new InvalidDataException(
                    LocalizationManager.GetString("Image.EmptyFile"));

            cancellationToken.ThrowIfCancellationRequested();
            using var imageStream = new MemoryStream(bytes, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.StreamSource = imageStream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}

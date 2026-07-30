using System.IO;
using System.Windows.Media.Imaging;

namespace CryptoBook.Interfaces
{
    public interface IImageContentLoader: IService
    {
        Task<BitmapSource> LoadFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        Task<BitmapSource> LoadAsync(
            Stream source,
            CancellationToken cancellationToken = default);
    }
}

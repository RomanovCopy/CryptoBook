using System.Windows.Media.Imaging;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Interfaces
{
    public interface IDocumentImageInserter: IService
    {
        Task<WpfImage> InsertAsync(
            BitmapSource source,
            CancellationToken cancellationToken = default);
    }
}

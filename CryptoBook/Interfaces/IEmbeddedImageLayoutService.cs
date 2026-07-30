using CryptoBook.DTO;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Interfaces
{
    public interface IEmbeddedImageLayoutService: IService
    {
        ImageLayoutMode GetLayout(WpfImage image);
        void SetLayout(WpfImage image, ImageLayoutMode mode);
        bool Remove(WpfImage image);
    }
}

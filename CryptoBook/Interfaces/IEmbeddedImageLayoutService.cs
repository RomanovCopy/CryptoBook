using CryptoBook.DTO;

using System.Windows.Documents;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Interfaces
{
    public interface IEmbeddedImageLayoutService: IService
    {
        ImageLayoutMode GetLayout(WpfImage image);
        void SetLayout(WpfImage image, ImageLayoutMode mode);
        TextPointer GetTextInsertionPosition(
            WpfImage image,
            ImageLayoutMode mode);
        bool Move(WpfImage image, TextPointer destination);
        bool Remove(WpfImage image);
    }
}

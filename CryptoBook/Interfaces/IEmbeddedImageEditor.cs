using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Interfaces
{
    public interface IEmbeddedImageEditor: IService
    {
        void ResizeToWidth(
            WpfImage image,
            double width,
            double maximumWidth);

        void FitToWidth(WpfImage image, double maximumWidth);

        void FitWithin(
            WpfImage image,
            double maximumWidth,
            double maximumHeight);
    }
}

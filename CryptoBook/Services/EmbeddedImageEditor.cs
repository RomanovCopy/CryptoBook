using CryptoBook.Interfaces;

using System.Windows.Controls;
using System.Windows.Media;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Services
{
    public sealed class EmbeddedImageEditor: IEmbeddedImageEditor
    {
        private readonly IDocumentSession? documentSession;

        public EmbeddedImageEditor(
            IDocumentSession? documentSession = null)
        {
            this.documentSession = documentSession;
        }

        public void ResizeToWidth(
            WpfImage image,
            double width,
            double maximumWidth)
        {
            ArgumentNullException.ThrowIfNull(image);

            if(!double.IsFinite(width) || width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if(!double.IsFinite(maximumWidth) || maximumWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumWidth));
            if(image.Source is null ||
               image.Source.Width <= 0 ||
               image.Source.Height <= 0)
            {
                throw new InvalidOperationException(
                    "У изображения отсутствует источник допустимого размера.");
            }

            double actualWidth = Math.Min(width, maximumWidth);
            double ratio = image.Source.Height / image.Source.Width;
            image.Stretch = Stretch.Uniform;
            image.StretchDirection = StretchDirection.Both;
            image.Width = actualWidth;
            image.Height = actualWidth * ratio;
            documentSession?.MarkDirty();
        }

        public void FitToWidth(WpfImage image, double maximumWidth)
        {
            ArgumentNullException.ThrowIfNull(image);
            if(image.Source is null)
                return;

            ResizeToWidth(
                image,
                Math.Min(image.Source.Width, maximumWidth),
                maximumWidth);
        }
    }
}

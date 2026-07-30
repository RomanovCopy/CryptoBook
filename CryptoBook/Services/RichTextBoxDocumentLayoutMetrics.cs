using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class RichTextBoxDocumentLayoutMetrics:
        IDocumentLayoutMetrics
    {
        private const double DefaultDocumentWidth = 720;
        private const double HorizontalReserve = 32;

        private readonly IRichTextBoxService richTextBox;

        public RichTextBoxDocumentLayoutMetrics(
            IRichTextBoxService richTextBox)
        {
            this.richTextBox = richTextBox
                ?? throw new ArgumentNullException(nameof(richTextBox));
        }

        public double AvailableWidth
        {
            get
            {
                double controlWidth = richTextBox.Service.ActualWidth;
                if(!double.IsFinite(controlWidth) || controlWidth <= 0)
                    controlWidth = DefaultDocumentWidth;

                System.Windows.Thickness padding =
                    richTextBox.Document.PagePadding;
                double width = controlWidth
                    - padding.Left
                    - padding.Right
                    - HorizontalReserve;
                return Math.Max(64, width);
            }
        }
    }
}

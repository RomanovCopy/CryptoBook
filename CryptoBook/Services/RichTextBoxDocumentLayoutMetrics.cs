using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class RichTextBoxDocumentLayoutMetrics:
        IDocumentLayoutMetrics
    {
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
                DocumentPageLayout.Apply(richTextBox.Document);
                return Math.Max(0, richTextBox.Document.PageWidth -
                    richTextBox.Document.PagePadding.Left -
                    richTextBox.Document.PagePadding.Right);
            }
        }

        public double AvailableHeight
        {
            get
            {
                DocumentPageLayout.Apply(richTextBox.Document);
                return double.PositiveInfinity;
            }
        }
    }
}

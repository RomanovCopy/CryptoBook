using System.Windows;
using System.Windows.Documents;
using CryptoBook.DTO;

namespace CryptoBook.Services
{
    public static class DocumentPageLayout
    {
        public static Thickness PagePadding { get; } =
            new(48);

        public static double GetWidth(DocumentPaperSize size, bool landscape = false)
        {
            (double shortSide, double longSide) = size switch
            {
                DocumentPaperSize.A2 => (420, 594),
                DocumentPaperSize.A3 => (297, 420),
                DocumentPaperSize.A4 => (210, 297),
                _ => throw new ArgumentOutOfRangeException(nameof(size))
            };
            return (landscape ? longSide : shortSide) * 96 / 25.4;
        }

        public static void Apply(FlowDocument document, DocumentPaperSize size, bool landscape)
        {
            ArgumentNullException.ThrowIfNull(document);
            document.MinPageWidth = 0;
            document.MaxPageWidth = double.PositiveInfinity;
            document.PageWidth = GetWidth(size, landscape);
            Apply(document);
        }

        public static void Apply(FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            double width = double.IsFinite(document.PageWidth) && document.PageWidth > 0
                ? document.PageWidth : GetWidth(DocumentPaperSize.A4);
            if(document.MinPageWidth != width || document.MaxPageWidth != width)
            {
                document.MinPageWidth = 0;
                document.MaxPageWidth = double.PositiveInfinity;
                document.MinPageWidth = width;
                document.MaxPageWidth = width;
            }
            document.PageWidth = width;
            document.PageHeight = double.NaN;
            document.PagePadding = PagePadding;
            document.ColumnWidth = double.PositiveInfinity;
        }
    }
}

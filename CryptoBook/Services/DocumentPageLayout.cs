using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public static class DocumentPageLayout
    {
        public static Thickness PagePadding { get; } =
            new(48);

        public static void Apply(FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            document.PageWidth = double.NaN;
            document.PageHeight = double.NaN;
            document.MinPageWidth = 0;
            document.MaxPageWidth = double.PositiveInfinity;
            document.PagePadding = PagePadding;
            document.ColumnWidth = double.PositiveInfinity;
        }
    }
}

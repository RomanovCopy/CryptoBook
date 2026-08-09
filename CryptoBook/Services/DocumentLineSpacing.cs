using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    internal static class DocumentLineSpacing
    {
        internal const double DefaultRatio = 1.2;
        internal const double MinimumRatio = 0.8;
        internal const double MaximumRatio = 3.0;
        internal const double Step = 0.1;

        internal static double NormalizeRatio(double ratio) =>
            double.IsNaN(ratio) || double.IsInfinity(ratio)
                ? DefaultRatio
                : Math.Clamp(ratio, MinimumRatio, MaximumRatio);

        internal static void Apply(FlowDocument document, double ratio)
        {
            ArgumentNullException.ThrowIfNull(document);
            ratio = NormalizeRatio(ratio);

            foreach(Paragraph paragraph in EnumerateParagraphs(document.Blocks))
                Apply(paragraph, ratio);
        }

        internal static void Apply(Paragraph paragraph, double ratio)
        {
            ArgumentNullException.ThrowIfNull(paragraph);
            ratio = NormalizeRatio(ratio);
            double fontSize = paragraph.FontSize > 0
                ? paragraph.FontSize
                : 12;

            paragraph.LineStackingStrategy = ratio < DefaultRatio
                ? LineStackingStrategy.BlockLineHeight
                : LineStackingStrategy.MaxHeight;
            paragraph.LineHeight = fontSize * ratio;
        }

        private static IEnumerable<Paragraph> EnumerateParagraphs(
            BlockCollection blocks)
        {
            foreach(Block block in blocks)
            {
                switch(block)
                {
                    case Paragraph paragraph:
                        yield return paragraph;
                        break;
                    case Section section:
                        foreach(var nested in EnumerateParagraphs(section.Blocks))
                            yield return nested;
                        break;
                    case System.Windows.Documents.List list:
                        foreach(ListItem item in list.ListItems)
                        foreach(var nested in EnumerateParagraphs(item.Blocks))
                            yield return nested;
                        break;
                    case Table table:
                        foreach(TableRowGroup group in table.RowGroups)
                        foreach(TableRow row in group.Rows)
                        foreach(TableCell cell in row.Cells)
                        foreach(var nested in EnumerateParagraphs(cell.Blocks))
                            yield return nested;
                        break;
                }
            }
        }
    }
}

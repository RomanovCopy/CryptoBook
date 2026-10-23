using CryptoBook.Services;
using System.Windows;
using System.Windows.Documents;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;

namespace CryptoBook.AI;

internal static class AiChatDocumentRenderer
{
    private static readonly MarkdownFlowDocumentRenderer Markdown = new();

    internal static FlowDocument Render(string source, bool renderMarkdown, FontFamily fontFamily, double fontSize, Brush foreground,
        string? codeFontFamily = null, string? codeColor = null)
    {
        FlowDocument document;
        try
        {
            // A chat has no source directory: Markdown images cannot read local files or fetch URLs.
            var codeFont = string.IsNullOrWhiteSpace(codeFontFamily) ? null : new FontFamily(codeFontFamily);
            var codeForeground = AiChatAppearance.ValidColor(codeColor)
                ? (Brush?)new System.Windows.Media.BrushConverter().ConvertFromInvariantString(codeColor!) : null;
            document = renderMarkdown ? Markdown.Render(source, null, codeFont, codeForeground) : PlainText(source);
        }
        catch(Exception error) when(error is ArgumentException or InvalidOperationException or NotSupportedException or FormatException)
        {
            document = PlainText(source);
        }
        if(renderMarkdown) ScaleFontSizes(document, fontSize / 15);
        document.MinPageWidth = 0;
        document.MaxPageWidth = double.PositiveInfinity;
        document.PageWidth = double.NaN;
        document.PagePadding = new Thickness(0);
        document.ColumnWidth = double.PositiveInfinity;
        document.FontFamily = fontFamily;
        document.FontSize = fontSize;
        document.Foreground = foreground;
        document.Background = Brushes.Transparent;
        return document;
    }

    private static FlowDocument PlainText(string source) => new(new Paragraph(new Run(source)) { Margin = new Thickness(0) });

    private static void ScaleFontSizes(DependencyObject parent, double scale)
    {
        foreach(object child in LogicalTreeHelper.GetChildren(parent))
        {
            if(child is not DependencyObject element) continue;
            if(element is TextElement text && text.ReadLocalValue(TextElement.FontSizeProperty) is double size)
                text.FontSize = size * scale;
            ScaleFontSizes(element, scale);
        }
    }
}

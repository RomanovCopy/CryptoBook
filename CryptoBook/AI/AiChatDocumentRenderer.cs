using CryptoBook.Infrastructure;
using CryptoBook.Services;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Binding = System.Windows.Data.Binding;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;

namespace CryptoBook.AI;

internal static class AiChatDocumentRenderer
{
    private static readonly MarkdownFlowDocumentRenderer Markdown = new();
    private static readonly ResourceWrapper Resources = new();

    internal static FlowDocument Render(string source, bool renderMarkdown, FontFamily fontFamily, double fontSize, Brush foreground,
        string? codeFontFamily = null, string? codeColor = null, ICommand? copyCodeCommand = null)
    {
        FlowDocument document;
        try
        {
            // A chat has no source directory: Markdown images cannot read local files or fetch URLs.
            var codeFont = string.IsNullOrWhiteSpace(codeFontFamily) ? null : new FontFamily(codeFontFamily);
            var codeForeground = AiChatAppearance.ValidColor(codeColor)
                ? (Brush?)new System.Windows.Media.BrushConverter().ConvertFromInvariantString(codeColor!) : null;
            document = renderMarkdown ? Markdown.Render(source, null, codeFont, codeForeground,
                (code, language) => RenderCodeBlock(code, language, codeFont, codeForeground, copyCodeCommand)) : PlainText(source);
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

    private static Section RenderCodeBlock(string code, string? language, FontFamily? fontFamily, Brush? foreground,
        ICommand? copyCodeCommand)
    {
        var section = new Section
        {
            Margin = new Thickness(0, 8, 0, 12),
            Padding = new Thickness(12, 5, 12, 9),
            BorderThickness = new Thickness(1)
        };
        section.SetResourceReference(TextElement.BackgroundProperty, "CurrentControlBackground");
        section.SetResourceReference(Block.BorderBrushProperty, "CurrentBorderColor");
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock
        {
            Text = language?.Trim() ?? "",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "CurrentMutedForeground");
        header.Children.Add(label);
        var icon = new System.Windows.Shapes.Path { Width = 16, Height = 16, Stretch = Stretch.Uniform };
        icon.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "Icon.Copy");
        icon.SetResourceReference(FrameworkElement.StyleProperty, "EditorToolbarIconStyle");
        var button = new Button
        {
            Name = "CopyCodeButton",
            Content = icon,
            Width = 28,
            Height = 28,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(5),
            Focusable = true,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Command = copyCodeCommand,
            CommandParameter = code
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "EditorToolButtonStyle");
        icon.SetBinding(System.Windows.Shapes.Path.StrokeProperty, new Binding(nameof(Button.Foreground)) { Source = button });
        button.SetBinding(FrameworkElement.ToolTipProperty, new Binding("[AI.CopyCode]") { Source = Resources });
        button.SetBinding(AutomationProperties.NameProperty, new Binding("[AI.CopyCode]") { Source = Resources });
        Grid.SetColumn(button, 1);
        header.Children.Add(button);
        section.Blocks.Add(new BlockUIContainer(header) { Margin = new Thickness(0, 0, 0, 4) });
        var paragraph = new Paragraph(new Run(code.TrimEnd('\r', '\n')))
        {
            FontFamily = fontFamily ?? new FontFamily(AiChatAppearance.DefaultCodeFontFamily),
            FontSize = 13,
            Margin = new Thickness(0)
        };
        if(foreground is not null) paragraph.Foreground = foreground;
        section.Blocks.Add(paragraph);
        return section;
    }

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

using CryptoBook.AI;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Services;

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class AiDocumentFragmentFormatterTests
{
    private const string Markdown = "Body `inline code` and [link](https://example.com/)\n\n" +
        "| Name | Cost |\n|---|---|\n| `Book` | 10 |\n\n> quote\n\n```cs\nint n = 1;\n```";

    [WpfTheory]
    [InlineData("#FFFFFFFF", "#FF000000")]
    [InlineData("#FF808080", "#FF000000")]
    [InlineData("#FF202124", "#FFF1F3F5")]
    [InlineData("#FFFFF4D6", "#FF1E4E38")]
    public async Task Insertion_UsesDocumentInkAndSubtlePaperTint_AfterSaveAndReopen(string paper, string ink)
    {
        var editor = TableEditingTests.CreateEditor(1, 1);
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(editor, markdown);
        session.Open("test.XamlPackage", new XamlPackageFileTemplate(), editor.Document);
        var document = editor.Document;
        document.Background = Brush(paper);
        document.Foreground = Brush(ink);
        editor.Selection.Select(document.ContentStart, document.ContentEnd);
        using var context = new AiEditorContext(editor, markdown, session, new AiFormattedInsertionTests.HomeNavigation());

        Assert.True(context.Capture()!.Apply(Markdown, true));
        AssertAppearance(document, paper, ink);
        var handler = new XamlPackageDocumentFormatHandler(new WpfDispatcherService(Dispatcher.CurrentDispatcher));
        var loaded = new FlowDocument();
        await handler.LoadAsync(loaded, await handler.SerializeAsync(document));
        AssertAppearance(loaded, paper, ink);
        Assert.Equal(Color(paper), Assert.IsType<SolidColorBrush>(loaded.Background).Color);
    }

    [WpfFact]
    public void Formatter_RemovesThemeExpressions_WithoutChangingPreviewOrPaper()
    {
        var renderer = new MarkdownFlowDocumentRenderer();
        var preview = renderer.Render(Markdown, null);
        ApplyDarkResources(preview);
        var fragment = renderer.Render(Markdown, null);
        ApplyDarkResources(fragment);
        var paper = new LinearGradientBrush(Colors.White, Colors.LightGray, 90);
        var target = new FlowDocument { Foreground = Brushes.Black, Background = paper };
        Assert.Equal(Color("#FF2B2D31"), Assert.IsType<SolidColorBrush>(FindInlineCode(fragment).Background).Color);

        AiDocumentFragmentFormatter.Apply(fragment, target);

        Assert.Null(FindInlineCode(fragment).Background);
        Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(FindInlineCode(fragment).Foreground).Color);
        Assert.Same(paper, target.Background);
        Assert.True(EffectiveAlpha(HeaderTint(fragment)) < 0.1);
        Assert.Equal(Color("#FF2B2D31"), Assert.IsType<SolidColorBrush>(FindInlineCode(preview).Background).Color);
        using(var stream = new MemoryStream())
        {
            new TextRange(fragment.ContentStart, fragment.Blocks.LastBlock!.ContentEnd).Save(stream, DataFormats.XamlPackage, true);
            stream.Position = 0;
            var loaded = new FlowDocument();
            new TextRange(loaded.ContentStart, loaded.ContentEnd).Load(stream, DataFormats.XamlPackage);
            var loadedLink = new FlowDocumentWalker().Find<Hyperlink>(loaded).Single();
            var linkInk = Assert.IsType<SolidColorBrush>(loadedLink.Foreground);
            Assert.Equal(Colors.Black.R, linkInk.Color.R);
            Assert.InRange(EffectiveAlpha(linkInk), 0.94, 0.96);
        }
        foreach(var element in new FlowDocumentWalker().Traverse(fragment).Cast<DependencyObject>().Prepend(fragment))
        {
            var values = element.GetLocalValueEnumerator();
            while(values.MoveNext())
                Assert.False(DependencyPropertyHelper.GetValueSource(element, values.Current.Property).IsExpression);
        }
    }

    private static void AssertAppearance(FlowDocument document, string paper, string ink)
    {
        var elements = new FlowDocumentWalker().Traverse(document).ToArray();
        var code = FindInlineCode(document);
        Assert.Contains("Consolas", code.FontFamily.Source);
        Assert.True(code.Background is null || code.Background.Opacity == 0 ||
            code.Background is SolidColorBrush { Color.A: 0 });
        Assert.All(elements.OfType<Run>(), run =>
        {
            var actual = Assert.IsType<SolidColorBrush>(run.Foreground);
            var expected = Color(ink);
            Assert.Equal(expected.R, actual.Color.R);
            Assert.Equal(expected.G, actual.Color.G);
            Assert.Equal(expected.B, actual.Color.B);
            Assert.InRange(EffectiveAlpha(actual), run.Parent is Hyperlink ? 0.94 : 0.99, 1);
        });
        var link = Assert.Single(elements.OfType<Hyperlink>());
        Assert.Equal(new Uri("https://example.com/"), link.NavigateUri);
        Assert.NotEmpty(link.TextDecorations);
        var tint = HeaderTint(document);
        double alpha = EffectiveAlpha(tint);
        Assert.InRange(alpha, 0.04, 0.08);
        var page = Color(paper);
        var shaded = System.Windows.Media.Color.FromRgb(
            (byte)Math.Round(tint.Color.R * alpha + page.R * (1 - alpha)),
            (byte)Math.Round(tint.Color.G * alpha + page.G * (1 - alpha)),
            (byte)Math.Round(tint.Color.B * alpha + page.B * (1 - alpha)));
        Assert.True(Contrast(Color(ink), shaded) >= 4.5);
        Assert.All(elements.OfType<TableCell>(), cell =>
            Assert.InRange(EffectiveAlpha(Assert.IsType<SolidColorBrush>(cell.BorderBrush)), 0.3, 0.34));
        Assert.All(elements.OfType<Block>().Where(block => block.Background is not null),
            block => Assert.InRange(EffectiveAlpha(Assert.IsType<SolidColorBrush>(block.Background)), 0, 0.08));
    }

    private static double EffectiveAlpha(SolidColorBrush brush) => brush.Opacity * brush.Color.A / 255;

    private static double Contrast(Color first, Color second)
    {
        static double Linear(byte value)
        {
            double channel = value / 255d;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static Run FindInlineCode(FlowDocument document) => new FlowDocumentWalker().Find<Run>(document)
        .Single(run => run.Text == "inline code");
    private static SolidColorBrush HeaderTint(FlowDocument document) => Assert.IsType<SolidColorBrush>(
        new FlowDocumentWalker().Find<Table>(document).Single().RowGroups[0].Rows[0].Cells[0].Background);
    private static Color Color(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static SolidColorBrush Brush(string value) => new(Color(value));
    private static void ApplyDarkResources(FlowDocument document)
    {
        document.Resources["CurrentWindowForeground"] = Brush("#FFF1F3F5");
        document.Resources["CurrentDocumentBackground"] = Brush("#FF202124");
        document.Resources["CurrentControlBackground"] = Brush("#FF2B2D31");
        document.Resources["CurrentBorderColor"] = Brush("#FFD0D7DE");
        document.Resources["CurrentAccent"] = Brush("#FF93C5FD");
        document.Resources["CurrentMutedForeground"] = Brush("#FFC5CBD3");
    }
}

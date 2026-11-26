using CryptoBook.AI;
using CryptoBook.Infrastructure;
using CryptoBook.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;

namespace CryptoBook.Tests;

public sealed class MarkdownMathTests
{
    internal const string ExampleReply = """
        Задача 1: $1, 2, 3, 4, 5, \text{?}$\
        • Ваш ответ: Последовательные числа.\
        • Результат: Верно. Следующее число — $\mathbf{6}$. (Простое приращение на 1).\
        Задача 2: $3, 6, 12, 24, \text{?}$\
        • Ваш ответ: Умножение на 2.\
        • Результат: Верно. Следующее число — $24 \times 2 = \mathbf{48}$.\
        Задача 3: $1, 3, 2, 5, 4, 7, \text{?}$\
        • Ряд А (по нечетным позициям): $1, 2, 4, \dots$ (Здесь закономерность $+1, +2, \dots$ или $2^n$?)\
        • Ряд Б (по четным позициям): $3, 5, 7, \dots$ (Простое приращение на 2).\
        • Если мы предполагаем, что ряд продолжается по схеме: $1 \xrightarrow{+2} 3 \xrightarrow{-1} 2 \xrightarrow{+3} 5 \xrightarrow{-1} 4 \xrightarrow{+3} 7$. Следующее число должно быть $4 + 2 = \mathbf{6}$.\
        Задача 4: $2, 5, 11, 23, \text{?}$\
        • $2 \xrightarrow{+3} 5$\
        • $5 \xrightarrow{+6} 11$\
        • $11 \xrightarrow{+12} 23$\
        • Следующее приращение должно быть $12 + 6 = 18$.\
        • $23 + 18 = \mathbf{41}$.\
        Задача 5: $1, 1, 2, 3, 5, 8, 13, \text{?}$\
        • Результат: Абсолютно верно. Это знаменитая последовательность Фибоначчи. $8 + 13 = \mathbf{21}$.
        """;

    [WpfTheory]
    [InlineData(@"1, 2, 3, 4, 5, \text{?}")]
    [InlineData(@"\mathbf{6}")]
    [InlineData(@"24 \times 2 = \mathbf{48}")]
    [InlineData(@"1 \xrightarrow{+2} 3 \xrightarrow{-1} 2")]
    [InlineData(@"A \xrightarrow[низ]{\text{верх}} B")]
    [InlineData(@"B \xleftarrow{+2} A")]
    [InlineData(@"\frac{1}{2}")]
    [InlineData(@"\sqrt{x^2 + 1}")]
    [InlineData(@"\begin{pmatrix}1 & 2 \\ 3 & 4\end{pmatrix}")]
    [InlineData(@"\sum_{i=1}^{n} i + \int_0^1 x^2 dx")]
    [InlineData(@"\text{Русский текст}")]
    public void Formula_TypesetsSupportedCommandsAsVectorDrawing(string latex)
    {
        var inline = Assert.IsType<InlineUIContainer>(MarkdownMathRenderer.Render(latex, "$" + latex + "$", false));
        var image = Assert.IsType<Image>(inline.Child);
        var source = Assert.IsType<DrawingImage>(image.Source);
        Assert.False(source.Drawing.Bounds.IsEmpty);
        Assert.True(source.Width > 0 && source.Height > 0);
        Assert.Equal("$" + latex + "$", image.ToolTip);
    }

    [WpfFact]
    public void SuppliedReply_RendersEveryFormulaAndHardLineBreak_WithoutChangingSource()
    {
        var document = Chat(ExampleReply);
        var formulas = Images(document);
        Assert.Equal(19, formulas.Length);
        Assert.All(formulas, image => Assert.IsType<DrawingImage>(image.Source));
        string displayed = Text(document);
        Assert.DoesNotContain('\\', displayed);
        Assert.DoesNotContain('$', displayed);
        for(int task = 1; task <= 5; task++) Assert.Contains($"Задача {task}:", displayed);
        Assert.Equal(17, new FlowDocumentWalker().Find<LineBreak>(document).Count());
        var raw = AiChatDocumentRenderer.Render(ExampleReply, false, new FontFamily("Segoe UI"), 15, Brushes.Black);
        Assert.Equal(ExampleReply, Text(raw).Replace("\r\n", "\n").TrimEnd('\n'));
    }

    [WpfTheory]
    [InlineData(@"\unknowncommand{x}")]
    [InlineData(@"\frac{1}{")]
    [InlineData(@"\input{private.txt}")]
    public void UnknownOrIncompleteCommand_StaysReadableAndDoesNotHideFollowingText(string latex)
    {
        string source = "До $" + latex + "$ после";
        var document = Chat(source);
        Assert.Empty(Images(document));
        Assert.Contains(source, Text(document));
    }

    [WpfFact]
    public void ExcessiveNestingOrLength_FallsBackToSourceBeforeEnteringTypesetter()
    {
        foreach(string latex in new[] { new string('{', 100) + "x" + new string('}', 100), new string('x', 10000),
            string.Concat(Enumerable.Repeat(@"\sqrt", 500)) + "x", new string('^', 500) + "x" })
            Assert.Equal(latex, Assert.IsType<Run>(MarkdownMathRenderer.Render(latex, latex, false)).Text);
    }

    [WpfFact]
    public void Formula_UsesChatInkAndScalesWithText_WhileCodeRemainsLiteral()
    {
        const string formula = "$\\frac{1}{2}$";
        var small = Chat(formula, 12, Brushes.DarkGreen);
        var large = Chat(formula, 24, Brushes.White);
        Assert.Equal(Images(small)[0].Width * 2, Images(large)[0].Width, 5);
        var geometries = Drawings(Assert.IsType<DrawingImage>(Images(large)[0].Source).Drawing).OfType<GeometryDrawing>();
        Assert.Contains(geometries, drawing => drawing.Brush == Brushes.White);
        var code = Chat("`" + formula + "`\n\n```tex\n" + formula + "\n```");
        Assert.Empty(Images(code));
        Assert.Equal(2, Text(code).Split(formula).Length - 1);
    }

    [WpfTheory]
    [InlineData("#FF111111")]
    [InlineData("#FFF0F0F0")]
    public async Task FormulaInsertion_SavesSharpImagesAndSourceTooltip_AndReopensWithoutMathControl(string ink)
    {
        const string source = "До $\\frac{1}{2}$ после\n\n$$\n\\sqrt{x^2 + 1}\n$$";
        var fragment = new MarkdownFlowDocumentRenderer().Render(source, null);
        var target = new FlowDocument { Foreground = (Brush)new BrushConverter().ConvertFromInvariantString(ink)! };
        AiDocumentFragmentFormatter.Apply(fragment, target);
        var images = Images(fragment);
        Assert.Equal(2, images.Length);
        foreach(var image in images)
        {
            var bitmap = Assert.IsType<RenderTargetBitmap>(image.Source);
            Assert.True(bitmap.DpiX >= 192);
            Assert.True(bitmap.PixelWidth >= image.Width * 2);
            AssertInk(bitmap, ((SolidColorBrush)target.Foreground).Color);
        }
        var handler = new XamlPackageDocumentFormatHandler(new WpfDispatcherService(Dispatcher.CurrentDispatcher));
        var loaded = new FlowDocument();
        await handler.LoadAsync(loaded, await handler.SerializeAsync(fragment));
        var reopened = Images(loaded);
        Assert.Equal(2, reopened.Length);
        Assert.Equal(images.Select(image => image.ToolTip), reopened.Select(image => image.ToolTip));
        Assert.All(reopened, image => Assert.IsAssignableFrom<BitmapSource>(image.Source));
        Assert.All(reopened, image => AssertInk((BitmapSource)image.Source, ((SolidColorBrush)target.Foreground).Color));
        Assert.Contains("До", Text(loaded));
        Assert.Contains("после", Text(loaded));
    }

    [WpfFact]
    public async Task LongFormula_FitsTheChatViewportWithoutHorizontalClipping()
    {
        var viewer = new RichTextBox { Document = Chat("$" + string.Join("+", Enumerable.Repeat("12345", 20)) + "$") };
        var window = new Window { Content = viewer, Width = 280, Height = 220, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            window.Show();
            await Task.Delay(100);
            window.UpdateLayout();
            var image = Assert.Single(Images(viewer.Document));
            Assert.True(image.ActualWidth > 0 && image.ActualWidth < viewer.ActualWidth);
            Assert.True(image.ActualHeight > 0);
        }
        finally { window.Close(); }
    }

    private static FlowDocument Chat(string source, double fontSize = 15, Brush? ink = null)
        => AiChatDocumentRenderer.Render(source, true, new FontFamily("Segoe UI"), fontSize, ink ?? Brushes.Black);
    private static Image[] Images(FlowDocument document) => new FlowDocumentWalker().Find<InlineUIContainer>(document)
        .Select(inline => inline.Child).OfType<Image>().ToArray();
    private static string Text(FlowDocument document) => new TextRange(document.ContentStart, document.ContentEnd).Text;
    private static void AssertInk(BitmapSource bitmap, Color expected)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        Assert.Contains(Enumerable.Range(0, pixels.Length / 4), pixel => pixels[pixel * 4 + 3] > 128 &&
            Math.Abs(pixels[pixel * 4] - expected.B) <= 1 && Math.Abs(pixels[pixel * 4 + 1] - expected.G) <= 1 &&
            Math.Abs(pixels[pixel * 4 + 2] - expected.R) <= 1);
    }
    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        if(drawing is DrawingGroup group)
            foreach(var child in group.Children)
                foreach(var descendant in Drawings(child)) yield return descendant;
    }
}

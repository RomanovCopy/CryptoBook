using CryptoBook.Behaviors;
using CryptoBook.Services;
using CryptoBook.DTO;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentPageLayoutTests
{
    [WpfFact]
    public void Apply_DefaultsToA4WithUnlimitedHeight()
    {
        var document = new FlowDocument();

        DocumentPageLayout.Apply(document);

        Assert.Equal(210 * 96 / 25.4, document.PageWidth);
        Assert.True(double.IsNaN(document.PageHeight));
        Assert.Equal(document.PageWidth, document.MinPageWidth);
        Assert.Equal(document.PageWidth, document.MaxPageWidth);
        Assert.Equal(DocumentPageLayout.PagePadding, document.PagePadding);
        Assert.True(double.IsPositiveInfinity(document.ColumnWidth));
    }

    [WpfTheory]
    [InlineData(DocumentPaperSize.A2, false, 420)]
    [InlineData(DocumentPaperSize.A2, true, 594)]
    [InlineData(DocumentPaperSize.A3, false, 297)]
    [InlineData(DocumentPaperSize.A3, true, 420)]
    [InlineData(DocumentPaperSize.A4, false, 210)]
    [InlineData(DocumentPaperSize.A4, true, 297)]
    public async Task PaperWidth_SurvivesSerializationAndPreview(
        DocumentPaperSize size, bool landscape, double millimeters)
    {
        var document = new FlowDocument(new Paragraph(new Run("page content")));
        DocumentPageLayout.Apply(document);
        DocumentPageLayout.Apply(document, size, landscape);
        var handler = new XamlPackageDocumentFormatHandler(
            new WpfDispatcherService(System.Windows.Threading.Dispatcher.CurrentDispatcher));
        byte[] content = await handler.SerializeAsync(document);
        var restored = new FlowDocument();
        await handler.LoadAsync(restored, content);
        DocumentPageLayout.Apply(restored);
        DocumentPageLayout.Apply(restored);

        Assert.Equal(millimeters * 96 / 25.4, restored.PageWidth, 6);
        Assert.Equal(restored.PageWidth, restored.MinPageWidth);
        Assert.Equal(restored.PageWidth, restored.MaxPageWidth);
        Assert.True(double.IsNaN(restored.PageHeight));
        var preview = new DocumentPreviewService().CreatePreview(restored);
        Assert.Equal(restored.PageWidth, preview.PageWidth);
        Assert.True(double.IsNaN(preview.PageHeight));
    }

    [WpfFact]
    public void Editor_KeepsPageWidthWhenViewportNarrowsAndContentGrows()
    {
        var document = new FlowDocument();
        DocumentPageLayout.Apply(document, DocumentPaperSize.A3, true);
        for(int i = 0; i < 100; i++)
            document.Blocks.Add(new Paragraph(new Run(new string('x', 120))));
        var editor = new RichTextBox(document)
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0), BorderThickness = new Thickness(0)
        };
        editor.Measure(new Size(600, 400));
        editor.Arrange(new Rect(0, 0, 600, 400));
        editor.UpdateLayout();
        Assert.InRange(editor.ExtentWidth, document.PageWidth - 1, document.PageWidth + 1);
        Assert.True(editor.ExtentHeight > 400);
        Assert.True(editor.ExtentWidth > editor.ViewportWidth);
        Assert.True(double.IsNaN(document.PageHeight));
    }

    [WpfFact]
    public void Editor_DrawsThreePageEdgesWithoutBottomEdge()
    {
        var document = new FlowDocument(new Paragraph(new Run("A4 — страница с неограниченной высотой")));
        DocumentPageLayout.Apply(document);
        var editor = new RichTextBox(document)
        {
            Padding = new Thickness(0), BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Black, Background = Brushes.White,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var host = new AdornerDecorator { Child = editor };
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        host.Measure(new Size(1000, 400));
        host.Arrange(new Rect(0, 0, 1000, 400));
        host.UpdateLayout();
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        host.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1000, 400, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var pixels = new byte[1000 * 400 * 4];
        bitmap.CopyPixels(pixels, 4000, 0);
        byte Red(int x, int y) => pixels[(y * 1000 + x) * 4 + 2];
        int gutter = (int)Math.Round((1000 - document.PageWidth) / 2);
        Assert.True(Red(gutter, 200) < 128, "Left page edge is visible.");
        Assert.True(Red(400, gutter) < 128, "Top page edge is visible.");
        Assert.True(Red(1000 - gutter - 1, 200) < 128, "Right page edge is visible.");
        Assert.Equal(255, Red(400, 399));
        if(Environment.GetEnvironmentVariable("CRYPTOBOOK_UI_QA_DIR") is { Length: > 0 } outputDirectory)
        {
            System.IO.Directory.CreateDirectory(outputDirectory);
            using var stream = System.IO.File.Create(System.IO.Path.Combine(outputDirectory, "page-editor.png"));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    [WpfFact]
    public void FitToWindow_UsesSmallerViewportDimension()
    {
        double zoom = DocumentPageFitBehavior.CalculateZoom(
            new Size(1200, 800),
            new Size(800, 1100),
            minimumZoom: 20,
            maximumZoom: 400);

        Assert.Equal(64, zoom);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Editor_CentersPaperWithEqualGuttersAfterResizeAndDocumentChange(bool longDocument)
    {
        var document = new FlowDocument();
        DocumentPageLayout.Apply(document);
        for(int i = 0; i < (longDocument ? 100 : 1); i++)
            document.Blocks.Add(new Paragraph(new Run("Page content")));
        var editor = new RichTextBox(document)
        {
            Padding = new Thickness(0), BorderThickness = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var content = new ContentControl { Content = editor };
        var host = new AdornerDecorator { Child = content };
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        Layout(1200);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        host.UpdateLayout();
        AssertCentered(1200);

        Layout(1050);
        AssertCentered(1050);
        if(longDocument)
        {
            editor.ScrollToVerticalOffset(100);
            host.UpdateLayout();
            Assert.True(editor.VerticalOffset > 0);
            AssertCentered(1050);
        }

        var wideDocument = new FlowDocument(new Paragraph(new Run("Wide page")));
        DocumentPageLayout.Apply(wideDocument, DocumentPaperSize.A2, true);
        editor.Document = wideDocument;
        host.UpdateLayout();
        Assert.Equal(new Thickness(24, 24, 24, 0), editor.Margin);
        Assert.True(editor.ExtentWidth > editor.ViewportWidth);
        editor.ScrollToHorizontalOffset(200);
        host.UpdateLayout();
        Assert.True(editor.HorizontalOffset > 0);
        Assert.Equal(DocumentPageLayout.GetWidth(DocumentPaperSize.A2, true), wideDocument.PageWidth);

        editor.Document = document;
        host.UpdateLayout();
        AssertCentered(1050);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(new Thickness(0), editor.Margin);

        void Layout(double width)
        {
            host.Measure(new Size(width, 800));
            host.Arrange(new Rect(0, 0, width, 800));
            host.UpdateLayout();
        }

        void AssertCentered(double width)
        {
            Point origin = editor.TranslatePoint(new Point(), host);
            double gutter = (width - document.PageWidth) / 2;
            Assert.Equal(gutter, origin.X, 4);
            Assert.Equal(gutter, origin.Y, 4);
            Assert.Equal(gutter, width - origin.X - document.PageWidth, 4);
            Assert.InRange(editor.ViewportWidth, document.PageWidth - 0.01, document.PageWidth + 0.01);
        }
    }

    [WpfFact]
    public void Preview_DoesNotResizeImagesToFixedPage()
    {
        var source = new FlowDocument();
        DocumentPageLayout.Apply(source);

        for(int index = 0; index < 3; index++)
        {
            var image = new Image
            {
                Source = CreateTallBitmap(),
                Stretch = Stretch.Uniform
            };

            source.Blocks.Add(
                new Paragraph(new InlineUIContainer(image))
                {
                    Margin = new Thickness(0)
                });
        }

        FlowDocument preview =
            new DocumentPreviewService().CreatePreview(source);
        Image[] previewImages = preview.Blocks
            .OfType<Paragraph>()
            .SelectMany(paragraph => paragraph.Inlines)
            .OfType<InlineUIContainer>()
            .Select(container => container.Child)
            .OfType<Image>()
            .ToArray();
        Assert.Equal(3, previewImages.Length);
        foreach(Image image in previewImages)
        {
            Assert.InRange(image.Source.Width, 999, 1001);
            Assert.InRange(image.Source.Height, 1999, 2001);
            Assert.True(double.IsNaN(image.Width));
            Assert.True(double.IsNaN(image.Height));
        }
    }

    [WpfFact]
    public void Preview_PreservesPaperBackground()
    {
        var source = new FlowDocument(new Paragraph(new Run("text")))
        {
            Background = Brushes.Bisque
        };

        FlowDocument preview =
            new DocumentPreviewService().CreatePreview(source);

        Assert.Equal(
            Colors.Bisque,
            Assert.IsType<SolidColorBrush>(preview.Background).Color);
    }

    private static BitmapSource CreateTallBitmap()
    {
        const int width = 1000;
        const int height = 2000;
        int stride = (width + 7) / 8;
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.BlackWhite,
            null,
            new byte[stride * height],
            stride);
        bitmap.Freeze();
        return bitmap;
    }
}

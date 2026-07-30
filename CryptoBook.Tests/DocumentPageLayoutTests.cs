using CryptoBook.Behaviors;
using CryptoBook.Services;

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
    public void Apply_UsesFlexiblePageSize()
    {
        var document = new FlowDocument();

        DocumentPageLayout.Apply(document);

        Assert.True(double.IsNaN(document.PageWidth));
        Assert.True(double.IsNaN(document.PageHeight));
        Assert.Equal(0, document.MinPageWidth);
        Assert.True(double.IsPositiveInfinity(document.MaxPageWidth));
        Assert.Equal(DocumentPageLayout.PagePadding, document.PagePadding);
        Assert.True(double.IsPositiveInfinity(document.ColumnWidth));
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

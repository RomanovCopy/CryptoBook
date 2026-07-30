using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentFormatHandlerTests
{
    [StaFact]
    public async Task XamlPackage_CreatesLoadableNonEmptyDocument()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());

        byte[] content = await new XamlPackageFileTemplate()
            .GetInitialContentAsync(CancellationToken.None);
        var document = new FlowDocument();

        await handler.LoadAsync(document, content);

        Assert.NotEmpty(content);
        Assert.NotNull(document.Blocks.FirstBlock);
    }

    [StaFact]
    public async Task Rtf_CreatesDocumentWithInitialText()
    {
        var handler = new RtfDocumentFormatHandler(CreateDispatcher());

        byte[] content = await new RichTextFileTemplate()
            .GetInitialContentAsync(CancellationToken.None);
        var document = new FlowDocument();
        await handler.LoadAsync(document, content);

        string text = new TextRange(
            document.ContentStart,
            document.ContentEnd).Text;

        Assert.NotEmpty(content);
        Assert.Contains("New document", text);
    }

    [StaFact]
    public async Task XamlPackage_PreservesEmbeddedImageDimensions()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());
        Image loadedImage = await RoundTripImage(handler);

        Assert.Equal(120, loadedImage.Width);
        Assert.Equal(60, loadedImage.Height);
        Assert.NotNull(loadedImage.Source);
    }

    [StaFact]
    public async Task Rtf_PreservesEmbeddedImageDimensions()
    {
        var handler = new RtfDocumentFormatHandler(CreateDispatcher());
        Image loadedImage = await RoundTripImage(handler);

        Assert.Equal(120, loadedImage.Width, precision: 1);
        Assert.Equal(60, loadedImage.Height, precision: 1);
        Assert.NotNull(loadedImage.Source);
    }

    [StaFact]
    public async Task XamlPackage_PreservesFloatingImageLayout()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());
        var sourceDocument = new FlowDocument();
        var image = new Image
        {
            Source = CreateBitmap(),
            Width = 120,
            Height = 60,
            Stretch = Stretch.Uniform
        };
        var figure = new Figure(new BlockUIContainer(image))
        {
            HorizontalAnchor = FigureHorizontalAnchor.ContentRight,
            VerticalAnchor = FigureVerticalAnchor.ParagraphTop,
            WrapDirection = WrapDirection.Left,
            CanDelayPlacement = false
        };
        var sourceParagraph = new Paragraph();
        sourceParagraph.Inlines.Add(new Run("до"));
        sourceParagraph.Inlines.Add(figure);
        sourceParagraph.Inlines.Add(new Run("после"));
        sourceDocument.Blocks.Add(sourceParagraph);

        byte[] content = await handler.SerializeAsync(sourceDocument);
        var loadedDocument = new FlowDocument();
        await handler.LoadAsync(loadedDocument, content);

        var paragraph =
            Assert.IsType<Paragraph>(loadedDocument.Blocks.FirstBlock);
        Figure loadedFigure = Assert.IsType<Figure>(
            paragraph.Inlines.OfType<Figure>().Single());
        var imageBlock =
            Assert.IsType<BlockUIContainer>(loadedFigure.Blocks.FirstBlock);

        Assert.IsType<Image>(imageBlock.Child);
        Assert.Equal(
            FigureHorizontalAnchor.ContentRight,
            loadedFigure.HorizontalAnchor);
        Assert.Equal(WrapDirection.Left, loadedFigure.WrapDirection);
    }

    private static async Task<Image> RoundTripImage(
        IDocumentFormatHandler handler)
    {
        var sourceDocument = new FlowDocument();
        var paragraph = new Paragraph();
        var image = new Image
        {
            Source = CreateBitmap(),
            Width = 120,
            Height = 60,
            Stretch = Stretch.Uniform
        };
        paragraph.Inlines.Add(new InlineUIContainer(image));
        sourceDocument.Blocks.Add(paragraph);

        byte[] content = await handler.SerializeAsync(sourceDocument);
        var loadedDocument = new FlowDocument();
        await handler.LoadAsync(loadedDocument, content);

        return FindFirstImage(loadedDocument);
    }

    private static IDispatcherService CreateDispatcher() =>
        new WpfDispatcherService(Dispatcher.CurrentDispatcher);

    private static BitmapSource CreateBitmap()
    {
        const int width = 2;
        const int height = 1;
        byte[] pixels =
        [
            0, 0, 255, 255,
            0, 255, 0, 255
        ];
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static Image FindFirstImage(FlowDocument document)
    {
        foreach(Block block in document.Blocks)
        {
            if(block is not Paragraph paragraph)
                continue;

            foreach(Inline inline in paragraph.Inlines)
            {
                if(inline is InlineUIContainer { Child: Image image })
                    return image;
            }
        }

        throw new Xunit.Sdk.XunitException(
            "В загруженном документе отсутствует изображение.");
    }
}

using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
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
        Assert.Contains(
            LocalizationManager.GetString("FileTemplate.NewDocument"),
            text);
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
    public async Task XamlPackage_PreservesDocumentBackgroundColor()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());
        var sourceDocument = new FlowDocument(
            new Paragraph(new Run("colored paper")))
        {
            Background = new SolidColorBrush(
                Color.FromArgb(0xCC, 0x12, 0x34, 0x56))
        };

        byte[] content = await handler.SerializeAsync(sourceDocument);
        var loadedDocument = new FlowDocument();
        await handler.LoadAsync(loadedDocument, content);

        Assert.Equal(
            Color.FromArgb(0xCC, 0x12, 0x34, 0x56),
            Assert.IsType<SolidColorBrush>(
                loadedDocument.Background).Color);
    }

    [StaFact]
    public async Task XamlPackage_EmbedsAndRestoresDocumentBackgroundImage()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());
        var sourceDocument = new FlowDocument(
            new Paragraph(new Run("image paper")))
        {
            Background = new ImageBrush(CreateBitmap())
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Right,
                AlignmentY = AlignmentY.Bottom,
                Opacity = 0.4
            }
        };

        byte[] content = await handler.SerializeAsync(sourceDocument);
        var loadedDocument = new FlowDocument();
        await handler.LoadAsync(loadedDocument, content);

        var brush = Assert.IsType<ImageBrush>(loadedDocument.Background);
        var bitmap = Assert.IsAssignableFrom<BitmapSource>(brush.ImageSource);
        Assert.Equal(2, bitmap.PixelWidth);
        Assert.Equal(1, bitmap.PixelHeight);
        Assert.Equal(Stretch.UniformToFill, brush.Stretch);
        Assert.Equal(AlignmentX.Right, brush.AlignmentX);
        Assert.Equal(AlignmentY.Bottom, brush.AlignmentY);
        Assert.Equal(0.4, brush.Opacity, precision: 3);
    }

    [Fact]
    public void XamlPackage_HandlerAlsoOwnsSecureDocumentPayloads()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());

        Assert.True(handler.CanHandle(new SecureFileTemplate()));
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

    [StaFact]
    public async Task XamlPackage_LoadsLegacyXamlPayload()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());
        var sourceDocument = new FlowDocument(
            new Paragraph(new Run("legacy xaml")));
        byte[] content;
        using(var stream = new MemoryStream())
        {
            new TextRange(
                sourceDocument.ContentStart,
                sourceDocument.ContentEnd).Save(
                    stream,
                    System.Windows.DataFormats.Xaml,
                    preserveTextElements: true);
            content = stream.ToArray();
        }
        var loadedDocument = new FlowDocument();

        await handler.LoadAsync(loadedDocument, content);

        Assert.Contains(
            "legacy xaml",
            new TextRange(
                loadedDocument.ContentStart,
                loadedDocument.ContentEnd).Text);
    }

    [StaFact]
    public async Task XamlPackage_InvalidBinary_DoesNotClearCurrentDocument()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());
        var document = new FlowDocument(
            new Paragraph(new Run("keep current content")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.LoadAsync(
                document,
                new byte[] { 0, 1, 2, 3, 4, 5 }));

        Assert.Contains(
            "keep current content",
            new TextRange(
                document.ContentStart,
                document.ContentEnd).Text);
    }

    [StaFact]
    public async Task XamlPackage_DoesNotPersistParagraphServiceType()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());
        var sourceDocument = new FlowDocument();
        sourceDocument.Blocks.Add(
            new ParagraphService
            {
                Inlines = { new Run("application paragraph") }
            });

        byte[] content = await handler.SerializeAsync(sourceDocument);
        string packageXaml = ReadPackageDocumentXaml(content);
        var loadedDocument = new FlowDocument();
        await handler.LoadAsync(loadedDocument, content);

        Assert.DoesNotContain("ParagraphService", packageXaml);
        Assert.IsType<Paragraph>(loadedDocument.Blocks.FirstBlock);
        Assert.Contains(
            "application paragraph",
            new TextRange(
                loadedDocument.ContentStart,
                loadedDocument.ContentEnd).Text);
    }

    [StaFact]
    public async Task XamlPackage_LoadsLegacyParagraphServicePackage()
    {
        var handler = new XamlPackageDocumentFormatHandler(
            CreateDispatcher());
        var legacyDocument = new FlowDocument();
        legacyDocument.Blocks.Add(
            new ParagraphService
            {
                Inlines = { new Run("legacy application paragraph") }
            });
        byte[] legacyContent;
        using(var stream = new MemoryStream())
        {
            new TextRange(
                legacyDocument.ContentStart,
                legacyDocument.ContentEnd).Save(
                    stream,
                    System.Windows.DataFormats.XamlPackage,
                    preserveTextElements: true);
            legacyContent = stream.ToArray();
        }
        Assert.Contains(
            "ParagraphService",
            ReadPackageDocumentXaml(legacyContent));
        var loadedDocument = new FlowDocument();

        await handler.LoadAsync(loadedDocument, legacyContent);

        Assert.IsType<Paragraph>(loadedDocument.Blocks.FirstBlock);
        Assert.Contains(
            "legacy application paragraph",
            new TextRange(
                loadedDocument.ContentStart,
                loadedDocument.ContentEnd).Text);
    }

    private static string ReadPackageDocumentXaml(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var archive = new System.IO.Compression.ZipArchive(
            stream,
            System.IO.Compression.ZipArchiveMode.Read);
        var entry = archive.GetEntry("Xaml/Document.xaml")
            ?? throw new Xunit.Sdk.XunitException(
                "Xaml/Document.xaml отсутствует в пакете.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
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

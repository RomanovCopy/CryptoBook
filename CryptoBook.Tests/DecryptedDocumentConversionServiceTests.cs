using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DecryptedDocumentConversionServiceTests
{
    [StaFact]
    public async Task XamlPackage_ToRtf_PreservesTextParagraphsAndBasicFormatting()
    {
        ConversionFixture fixture = CreateFixture();
        var sourceDocument = new FlowDocument();
        var first = new Paragraph();
        first.Inlines.Add(new Bold(new Run("important")));
        sourceDocument.Blocks.Add(first);
        sourceDocument.Blocks.Add(new Paragraph(new Run("second paragraph")));
        byte[] source = await fixture.Xaml.SerializeAsync(sourceDocument);
        using var input = new MemoryStream(source, writable: false);
        using var output = new MemoryStream();

        await fixture.Service.ConvertAsync(
            input,
            ".XamlPackage",
            DecryptionOutputFormat.Rtf,
            output);

        byte[] rtf = output.ToArray();
        Assert.Contains(@"\b", Encoding.ASCII.GetString(rtf));
        var restored = new FlowDocument();
        await fixture.Rtf.LoadAsync(restored, rtf);
        string text = new TextRange(
            restored.ContentStart,
            restored.ContentEnd).Text;
        Assert.Contains("important", text);
        Assert.Contains("second paragraph", text);
        Assert.Equal(2, restored.Blocks.Count);
    }

    [StaFact]
    public async Task XamlPackage_ToPlainText_KeepsTextAndRemovesEmbeddedObjects()
    {
        ConversionFixture fixture = CreateFixture();
        var image = new Image
        {
            Source = CreateBitmap(),
            Width = 2,
            Height = 1
        };
        var paragraph = new Paragraph(new Run("visible text"));
        paragraph.Inlines.Add(new InlineUIContainer(image));
        var sourceDocument = new FlowDocument(paragraph);
        byte[] source = await fixture.Xaml.SerializeAsync(sourceDocument);
        using var input = new MemoryStream(source, writable: false);
        using var output = new MemoryStream();

        await fixture.Service.ConvertAsync(
            input,
            ".XamlPackage",
            DecryptionOutputFormat.PlainText,
            output);

        byte[] plainText = output.ToArray();
        var restored = new FlowDocument();
        await fixture.PlainText.LoadAsync(restored, plainText);
        string text = new TextRange(
            restored.ContentStart,
            restored.ContentEnd).Text;
        Assert.Contains("visible text", text);
        Assert.Empty(restored.Blocks
            .OfType<Paragraph>()
            .SelectMany(block => block.Inlines.OfType<InlineUIContainer>()));
    }

    [Fact]
    public void CanConvert_RejectsUnknownAndOutOfScopeFormats()
    {
        ConversionFixture fixture = CreateFixture();

        Assert.True(fixture.Service.CanConvert(".XamlPackage"));
        Assert.True(fixture.Service.CanConvert(".rtf"));
        Assert.True(fixture.Service.CanConvert(".txt"));
        Assert.False(fixture.Service.CanConvert(".png"));
        Assert.False(fixture.Service.CanConvert(".md"));
        Assert.False(fixture.Service.CanConvert(".docx"));
    }

    private static ConversionFixture CreateFixture()
    {
        var dispatcher = new WpfDispatcherService(
            Dispatcher.CurrentDispatcher);
        var xaml = new XamlPackageDocumentFormatHandler(dispatcher);
        var rtf = new RtfDocumentFormatHandler(dispatcher);
        var plainText = new PlainTextDocumentFormatHandler(dispatcher);
        var handlers = new DocumentFormatHandlerRegistry(
            [xaml, rtf, plainText]);
        var templates = new FileTemplateRegistry(
            [
                new XamlPackageFileTemplate(),
                new RichTextFileTemplate(),
                new PlainTextTemplate()
            ]);
        return new ConversionFixture(
            new DecryptedDocumentConversionService(
                templates,
                handlers,
                dispatcher),
            xaml,
            rtf,
            plainText);
    }

    private static BitmapSource CreateBitmap()
    {
        byte[] pixels = [0, 0, 255, 255, 0, 255, 0, 255];
        BitmapSource bitmap = BitmapSource.Create(
            2,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            8);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed record ConversionFixture(
        DecryptedDocumentConversionService Service,
        XamlPackageDocumentFormatHandler Xaml,
        RtfDocumentFormatHandler Rtf,
        PlainTextDocumentFormatHandler PlainText);
}

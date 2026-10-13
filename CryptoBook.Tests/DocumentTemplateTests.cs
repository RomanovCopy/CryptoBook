using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Globalization;
using System.Text;
using System.Windows.Documents;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentTemplateTests
{
    [Fact]
    public void Catalog_UsesEmptyDocumentForMissingOrUnknownDefault()
    {
        var catalog = new DocumentTemplateCatalog();
        Assert.Equal(6, catalog.Templates.Select(template => template.Id).Distinct().Count());
        Assert.Equal(DocumentTemplate.EmptyId, catalog.GetById(null).Id);
        Assert.Equal(DocumentTemplate.EmptyId, catalog.GetById("removed-template").Id);
        Assert.Empty(catalog.GetById(DocumentTemplate.EmptyId).Markdown);
    }

    [Theory]
    [InlineData("en-US", "Notes")]
    [InlineData("ru-RU", "Заметки")]
    [InlineData("uk-UA", "Нотатки")]
    [InlineData("de-DE", "Notizen")]
    public void Catalog_LocalizesNamesDescriptionsAndContent(string culture, string notesName)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var catalog = new DocumentTemplateCatalog();
            Assert.Equal(notesName, catalog.GetById("notes").DisplayName);
            foreach(DocumentTemplate template in catalog.Templates)
            {
                Assert.DoesNotContain("DocumentTemplate.", template.DisplayName);
                Assert.DoesNotContain("DocumentTemplate.", template.Description);
                if(template.Id != DocumentTemplate.EmptyId)
                    Assert.StartsWith("# " + template.DisplayName, template.Markdown);
            }
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [WpfTheory]
    [InlineData(DocumentPaperSize.A2, false)]
    [InlineData(DocumentPaperSize.A2, true)]
    [InlineData(DocumentPaperSize.A3, false)]
    [InlineData(DocumentPaperSize.A3, true)]
    [InlineData(DocumentPaperSize.A4, false)]
    [InlineData(DocumentPaperSize.A4, true)]
    public async Task Templates_RoundTripThroughExistingFormatsAndPaperSizes(DocumentPaperSize size, bool landscape)
    {
        var catalog = new DocumentTemplateCatalog();
        var handlers = CreateHandlers();
        var service = CreateService(catalog, handlers);
        IFileTemplate[] formats = [new PlainTextTemplate(), new MarkdownFileTemplate(),
            new RichTextFileTemplate(), new XamlFileTemplate(), new XamlPackageFileTemplate(), new SecureFileTemplate()];
        foreach(DocumentTemplate template in catalog.Templates)
        foreach(IFileTemplate format in formats)
        {
            byte[] content = await service.CreateContentAsync(template, format, size, landscape);
            if(format is MarkdownFileTemplate)
            {
                Assert.Equal(template.Markdown, Encoding.UTF8.GetString(content));
                continue;
            }
            var restored = new FlowDocument();
            await handlers.Find(format is SecureFileTemplate ? new XamlPackageFileTemplate() : format)!
                .LoadAsync(restored, content);
            string text = new TextRange(restored.ContentStart, restored.ContentEnd).Text;
            if(template.Id == DocumentTemplate.EmptyId)
                Assert.True(string.IsNullOrWhiteSpace(text));
            else
            {
                Assert.Contains(template.DisplayName, text);
                Assert.DoesNotContain("# ", text);
                if(format is RichTextFileTemplate or XamlPackageFileTemplate or SecureFileTemplate)
                {
                    Assert.True(restored.Blocks.Count > 3);
                    Assert.True(((Paragraph)restored.Blocks.FirstBlock).FontSize >= 25);
                }
            }
            if(format is XamlPackageFileTemplate or SecureFileTemplate)
            {
                Assert.Equal(DocumentPageLayout.GetWidth(size, landscape), restored.PageWidth);
                Assert.True(double.IsNaN(restored.PageHeight));
                // Редактор применяет поля при подключении загруженного документа.
                DocumentPageLayout.Apply(restored);
                Assert.Equal(DocumentPageLayout.PagePadding, restored.PagePadding);
            }
        }
    }

    [WpfFact]
    public async Task CreateContent_CancellationPreventsGeneration()
    {
        var catalog = new DocumentTemplateCatalog();
        var service = CreateService(catalog, CreateHandlers());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateContentAsync(
            catalog.GetById("report"), new SecureFileTemplate(), DocumentPaperSize.A4, false, cancellation.Token));
    }

    internal static DocumentFormatHandlerRegistry CreateHandlers()
    {
        var dispatcher = new WpfDispatcherService(Dispatcher.CurrentDispatcher);
        return new DocumentFormatHandlerRegistry([new PlainTextDocumentFormatHandler(dispatcher),
            new MarkdownDocumentFormatHandler(dispatcher), new RtfDocumentFormatHandler(dispatcher),
            new XamlTextDocumentFormatHandler(dispatcher), new XamlPackageDocumentFormatHandler(dispatcher)]);
    }

    internal static DocumentTemplateService CreateService(IDocumentTemplateCatalog catalog,
        IDocumentFormatHandlerRegistry handlers) => new(catalog, new MarkdownFlowDocumentRenderer(),
            handlers, new WpfDispatcherService(Dispatcher.CurrentDispatcher));
}

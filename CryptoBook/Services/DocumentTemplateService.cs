using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System.Text;
using System.Windows.Documents;
using Brushes = System.Windows.Media.Brushes;

namespace CryptoBook.Services;

/// <summary>Готовит содержимое до записи файла, используя существующие кодеки документов.</summary>
public sealed class DocumentTemplateService(
    IDocumentTemplateCatalog catalog,
    IMarkdownFlowDocumentRenderer renderer,
    IDocumentFormatHandlerRegistry formatHandlers,
    IDispatcherService dispatcher): IDocumentTemplateService
{
    public async Task<byte[]> CreateContentAsync(DocumentTemplate template, IFileTemplate format,
        DocumentPaperSize paperSize, bool isLandscape, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(format);
        cancellationToken.ThrowIfCancellationRequested();
        string markdown = catalog.GetById(template.Id).Markdown;
        if(format is MarkdownFileTemplate)
            return new UTF8Encoding(false).GetBytes(markdown);

        // Защищённые текстовые документы содержат XamlPackage внутри контейнера.
        IFileTemplate contentFormat = format is SecureFileTemplate ? new XamlPackageFileTemplate() : format;
        IDocumentFormatHandler handler = formatHandlers.Find(contentFormat)
            ?? throw new NotSupportedException($"Unsupported document format: {format.Id}");
        FlowDocument document = await dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            FlowDocument result = markdown.Length == 0
                ? new FlowDocument(new Paragraph()) : renderer.Render(markdown, null);
            // Цвета документа не зависят от текущей темы интерфейса.
            result.Foreground = Brushes.Black;
            result.Background = Brushes.White;
            DocumentPageLayout.Apply(result, paperSize, isLandscape);
            return result;
        });
        return await handler.SerializeAsync(document, cancellationToken);
    }
}

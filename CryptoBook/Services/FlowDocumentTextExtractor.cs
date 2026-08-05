using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System.IO;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public sealed class FlowDocumentTextExtractor: IDocumentTextExtractor
    {
        private readonly IDocumentFormatHandlerRegistry formatHandlers;
        private readonly IFileTemplateRegistry templateRegistry;
        private readonly IDispatcherService dispatcher;

        public FlowDocumentTextExtractor(
            IDocumentFormatHandlerRegistry formatHandlers,
            IFileTemplateRegistry templateRegistry,
            IDispatcherService dispatcher)
        {
            this.formatHandlers = formatHandlers ??
                throw new ArgumentNullException(nameof(formatHandlers));
            this.templateRegistry = templateRegistry ??
                throw new ArgumentNullException(nameof(templateRegistry));
            this.dispatcher = dispatcher ??
                throw new ArgumentNullException(nameof(dispatcher));
        }

        public bool CanExtract(string extension) =>
            FindTemplate(extension) is not null;

        public async Task<string> ExtractAsync(
            Stream content,
            string extension,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            IFileTemplate template = FindTemplate(extension) ??
                throw new NotSupportedException(
                    $"The '{extension}' format is not supported by this extractor.");
            IDocumentFormatHandler handler = formatHandlers.Find(template) ??
                throw new NotSupportedException(
                    $"No document handler is registered for '{extension}'.");

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var document = new FlowDocument();
            await handler.LoadAsync(
                document,
                buffer.ToArray(),
                cancellationToken);

            return await dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new TextRange(
                    document.ContentStart,
                    document.ContentEnd).Text;
            });
        }

        private IFileTemplate? FindTemplate(string extension) =>
            templateRegistry.GetAll().FirstOrDefault(template =>
                template is RichTextFileTemplate or XamlPackageFileTemplate &&
                template.CanHandleExtension(extension));
    }
}

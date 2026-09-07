using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System.Windows.Documents;

namespace CryptoBook.Services
{
    public sealed class MarkdownDocumentFormatHandler:
        IDocumentFormatHandler
    {
        private readonly IDispatcherService dispatcher;

        public MarkdownDocumentFormatHandler(IDispatcherService dispatcher)
        {
            this.dispatcher = dispatcher ??
                throw new ArgumentNullException(nameof(dispatcher));
        }

        public bool CanHandle(IFileTemplate template) =>
            template is MarkdownFileTemplate;

        public Task LoadAsync(
            FlowDocument document,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);
            MarkdownTextDocument source = MarkdownTextCodec.Decode(content);
            return dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                document.Blocks.Clear();
                document.Blocks.Add(new Paragraph(new Run(source.Text)));
                MarkdownDocumentMetadata.SetSource(document, source);
            });
        }

        public Task<byte[]> SerializeAsync(
            FlowDocument document,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);
            cancellationToken.ThrowIfCancellationRequested();
            MarkdownTextDocument source =
                MarkdownDocumentMetadata.GetSource(document)
                ?? throw new InvalidOperationException(
                    "FlowDocument is not backed by Markdown source text.");
            return Task.FromResult(MarkdownTextCodec.Encode(source));
        }
    }
}

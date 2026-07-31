using CryptoBook.Interfaces;

using System.IO;
using System.Linq;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public abstract class TextRangeDocumentFormatHandler<TTemplate>:
        IDocumentFormatHandler
        where TTemplate: IFileTemplate
    {
        private readonly IDispatcherService dispatcher;

        protected TextRangeDocumentFormatHandler(
            IDispatcherService dispatcher)
        {
            this.dispatcher = dispatcher
                ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        protected abstract string DataFormat { get; }
        protected virtual bool PreserveTextElements => true;
        protected virtual string ResolveLoadDataFormat(
            ReadOnlySpan<byte> content) => DataFormat;
        protected virtual byte[] PrepareLoadContent(byte[] content) =>
            content;

        public bool CanHandle(IFileTemplate template) => template is TTemplate;

        public Task LoadAsync(
            FlowDocument document,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            byte[] buffer = PrepareLoadContent(content.ToArray());

            return dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(buffer.Length == 0)
                {
                    document.Blocks.Clear();
                    document.Blocks.Add(new Paragraph());
                    return;
                }

                using var stream = new MemoryStream(buffer, writable: false);
                var loadedDocument = new FlowDocument();
                var range = new TextRange(
                    loadedDocument.ContentStart,
                    loadedDocument.ContentEnd);
                range.Load(
                    stream,
                    ResolveLoadDataFormat(buffer));

                document.Blocks.Clear();
                foreach(Block block in loadedDocument.Blocks.ToList())
                {
                    loadedDocument.Blocks.Remove(block);
                    document.Blocks.Add(block);
                }

                if(document.Blocks.Count == 0)
                    document.Blocks.Add(new Paragraph());
            });
        }

        public Task<byte[]> SerializeAsync(
            FlowDocument document,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            return dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Serialize(document);
            });
        }

        private byte[] Serialize(FlowDocument document)
        {
            using var stream = new MemoryStream();
            var range = new TextRange(
                document.ContentStart,
                document.ContentEnd);
            range.Save(
                stream,
                DataFormat,
                preserveTextElements: PreserveTextElements);
            return stream.ToArray();
        }
    }
}

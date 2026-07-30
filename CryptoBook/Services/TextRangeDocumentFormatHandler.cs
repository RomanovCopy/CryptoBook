using CryptoBook.Interfaces;

using System.IO;
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
        public bool CanHandle(IFileTemplate template) => template is TTemplate;

        public Task LoadAsync(
            FlowDocument document,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            byte[] buffer = content.ToArray();

            return dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                document.Blocks.Clear();

                if(buffer.Length == 0)
                {
                    document.Blocks.Add(new Paragraph());
                    return;
                }

                using var stream = new MemoryStream(buffer, writable: false);
                var range = new TextRange(
                    document.ContentStart,
                    document.ContentEnd);
                range.Load(stream, DataFormat);
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
            range.Save(stream, DataFormat, preserveTextElements: true);
            return stream.ToArray();
        }
    }
}

using CryptoBook.Interfaces;
using CryptoBook.Infrastructure;

using System.IO;
using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public sealed class DocumentPreviewService: IDocumentPreviewService
    {
        public FlowDocument CreatePreview(FlowDocument source)
        {
            ArgumentNullException.ThrowIfNull(source);

            using var stream = new PooledMemoryStream();
            var sourceRange = new TextRange(source.ContentStart, source.ContentEnd);
            sourceRange.Save(stream, System.Windows.DataFormats.XamlPackage);
            stream.Position = 0;

            var preview = new FlowDocument();
            DocumentPageLayout.Apply(preview);

            var previewRange = new TextRange(preview.ContentStart, preview.ContentEnd);
            previewRange.Load(stream, System.Windows.DataFormats.XamlPackage);
            DocumentPageLayout.Apply(preview);
            preview.Background = source.Background;
            return preview;
        }
    }
}

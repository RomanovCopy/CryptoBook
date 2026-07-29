using CryptoBook.Interfaces;

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

            using var stream = new MemoryStream();
            var sourceRange = new TextRange(source.ContentStart, source.ContentEnd);
            sourceRange.Save(stream, System.Windows.DataFormats.XamlPackage);
            stream.Position = 0;

            var preview = new FlowDocument
            {
                PagePadding = new Thickness(48),
                ColumnWidth = double.PositiveInfinity
            };

            var previewRange = new TextRange(preview.ContentStart, preview.ContentEnd);
            previewRange.Load(stream, System.Windows.DataFormats.XamlPackage);
            preview.ColumnWidth = double.PositiveInfinity;
            return preview;
        }
    }
}

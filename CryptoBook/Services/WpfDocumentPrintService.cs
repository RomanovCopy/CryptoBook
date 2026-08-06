using CryptoBook.Interfaces;

using System.Windows.Documents;

using WpfPrintDialog = System.Windows.Controls.PrintDialog;
using WpfSize = System.Windows.Size;

namespace CryptoBook.Services
{
    public sealed class WpfDocumentPrintService: IDocumentPrintService
    {
        private readonly IDocumentPreviewService previewService;

        public WpfDocumentPrintService(
            IDocumentPreviewService previewService)
        {
            this.previewService = previewService ??
                throw new ArgumentNullException(nameof(previewService));
        }

        public void Print(FlowDocument document, string documentName)
        {
            ArgumentNullException.ThrowIfNull(document);

            var dialog = new WpfPrintDialog();
            if(dialog.ShowDialog() != true)
                return;

            FlowDocument printableDocument =
                previewService.CreatePreview(document);
            double width = GetPrintableDimension(
                dialog.PrintableAreaWidth,
                printableDocument.PageWidth);
            double height = GetPrintableDimension(
                dialog.PrintableAreaHeight,
                printableDocument.PageHeight);

            printableDocument.PageWidth = width;
            printableDocument.PageHeight = height;
            printableDocument.ColumnWidth = double.PositiveInfinity;

            DocumentPaginator paginator =
                ((IDocumentPaginatorSource)printableDocument)
                    .DocumentPaginator;
            paginator.PageSize = new WpfSize(width, height);
            dialog.PrintDocument(
                paginator,
                string.IsNullOrWhiteSpace(documentName)
                    ? "CryptoBook"
                    : documentName);
        }

        private static double GetPrintableDimension(
            double printableDimension,
            double documentDimension)
        {
            if(double.IsFinite(printableDimension) &&
               printableDimension > 0)
            {
                return printableDimension;
            }

            if(double.IsFinite(documentDimension) && documentDimension > 0)
                return documentDimension;

            return 96;
        }
    }
}

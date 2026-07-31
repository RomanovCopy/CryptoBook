using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class PlainTextDocumentFormatHandler:
        TextRangeDocumentFormatHandler<PlainTextTemplate>
    {
        public PlainTextDocumentFormatHandler(IDispatcherService dispatcher)
            : base(dispatcher)
        {
        }

        protected override string DataFormat =>
            System.Windows.DataFormats.Text;
    }
}

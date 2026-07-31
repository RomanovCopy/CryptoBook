using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class XamlTextDocumentFormatHandler:
        TextRangeDocumentFormatHandler<XamlFileTemplate>
    {
        public XamlTextDocumentFormatHandler(IDispatcherService dispatcher)
            : base(dispatcher)
        {
        }

        protected override string DataFormat =>
            System.Windows.DataFormats.Text;
    }
}

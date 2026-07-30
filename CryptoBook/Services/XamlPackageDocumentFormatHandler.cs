using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System.Windows;

namespace CryptoBook.Services
{
    public sealed class XamlPackageDocumentFormatHandler:
        TextRangeDocumentFormatHandler<XamlPackageFileTemplate>
    {
        public XamlPackageDocumentFormatHandler(IDispatcherService dispatcher)
            : base(dispatcher)
        {
        }

        protected override string DataFormat =>
            System.Windows.DataFormats.XamlPackage;
    }
}

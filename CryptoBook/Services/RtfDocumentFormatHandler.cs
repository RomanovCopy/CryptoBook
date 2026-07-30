using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System.Windows;

namespace CryptoBook.Services
{
    public sealed class RtfDocumentFormatHandler:
        TextRangeDocumentFormatHandler<RichTextFileTemplate>
    {
        public RtfDocumentFormatHandler(IDispatcherService dispatcher)
            : base(dispatcher)
        {
        }

        protected override string DataFormat => System.Windows.DataFormats.Rtf;
    }
}

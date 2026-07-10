using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public class FlowDocumentLoadService:IFlowDocumentLoadService
    {
        private readonly IDispatcherService _dispatcherService;

        public FlowDocumentLoadService(IDispatcherService dispatcherService)
        {
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        }

        public async Task LoadAsync(IRichTextBoxService richTextBoxService, Stream source, IFileTemplate template, 
        CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(richTextBoxService);
            ArgumentNullException.ThrowIfNull(source);

            var document = richTextBoxService.Document;

            byte[] buffer;

            using(var memory = new MemoryStream())
            {
                await source.CopyToAsync(memory, cancellationToken);
                buffer = memory.ToArray();
            }

            await _dispatcherService.InvokeAsync(() =>
            {
                using var stream = new MemoryStream(buffer, writable: false);

                document.Blocks.Clear();

                TextRange range = new TextRange( document.ContentStart, document.ContentEnd);

                range.Load(stream, ToDataFormat(template));
            });
        }


        private static string ToDataFormat(IFileTemplate template)
        {

            return template switch
            {
                XamlFileTemplate => DataFormats.Text,
                RichTextFileTemplate => DataFormats.Rtf,
                PlainTextTemplate => DataFormats.Text,
                ImageFileTemplate => DataFormats.Bitmap,
                SecureFileTemplate => System.Windows.DataFormats.XamlPackage,
                XamlPackageFileTemplate => System.Windows.DataFormats.XamlPackage,
                _ => throw new NotSupportedException($"The template type '{template.GetType().Name}' is not supported."),
            };

        }

    }
}

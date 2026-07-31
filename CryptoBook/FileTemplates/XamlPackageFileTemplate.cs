using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.FileTemplates
{
    public class XamlPackageFileTemplate:IFileTemplate
    {
        public string Id => "XamlPackage";
        public string DisplayName => "Xaml Package файл";
        public string DefaultExtension => ".XamlPackage";
        public string SuggestedBaseName => "New XamlPackage";
        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var document = new FlowDocument(new Paragraph());
            using var stream = new MemoryStream();
            var range = new TextRange(
                document.ContentStart,
                document.ContentEnd);
            range.Save(
                stream,
                System.Windows.DataFormats.XamlPackage,
                preserveTextElements: true);
            return Task.FromResult(stream.ToArray());
        }

        public IReadOnlyCollection<string> Extensions =>
        [
            ".XamlPackage"
        ];
    }
}

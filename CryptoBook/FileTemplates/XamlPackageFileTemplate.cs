using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public class XamlPackageFileTemplate:IFileTemplate
    {
        public string Id => "XamlPackage";
        public string DisplayName => "Xaml Package файл";
        public string DefaultExtension => ".XamlPackage";
        public string SuggestedBaseName => "New XamlPackage";
        public Encoding? DefaultEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); // UTF-8 BOM

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct) => Task.FromResult(Array.Empty<byte>());

        public IReadOnlyCollection<string> Extensions =>
        [
            ".XamlPackage"
        ];
    }
}

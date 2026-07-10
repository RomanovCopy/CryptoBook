using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public class XamlFileTemplate:IFileTemplate
    {
        public string Id => "Xaml";
        public string DisplayName => "Xaml файл";
        public string DefaultExtension => ".xaml";
        public string SuggestedBaseName => "New file";
        public Encoding? DefaultEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); // UTF-8 BOM

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct) => Task.FromResult(Array.Empty<byte>());

        public IReadOnlyCollection<string> Extensions =>
        [
            ".xaml",
        ];
    }
}

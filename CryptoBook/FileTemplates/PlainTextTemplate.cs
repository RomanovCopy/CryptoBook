using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public class PlainTextTemplate:IFileTemplate
    {
        public string Id => "Text";
        public string DisplayName =>
            CryptoBook.Infrastructure.LocalizationManager.GetString(
                "FileTemplate.Text");
        public string DefaultExtension => ".txt";
        public string SuggestedBaseName =>
            CryptoBook.Infrastructure.LocalizationManager.GetString(
                "FileTemplate.NewFile");
        public Encoding? DefaultEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); // UTF-8 BOM

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct) => Task.FromResult(Array.Empty<byte>());

        public IReadOnlyCollection<string> Extensions =>
        [
            ".txt",
            ".log",
            ".md",
            ".cs",
            ".xaml",
            ".json",
            ".xml"
        ];
    }
}

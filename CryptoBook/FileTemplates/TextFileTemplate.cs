using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public sealed class TextFileTemplate: IFileTemplate
    {
        public string Id => "text";
        public string DisplayName => "Текстовый файл";
        public string DefaultExtension => ".txt";
        public string SuggestedBaseName => "New file";
        public Encoding? DefaultEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); // UTF-8 BOM

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
            => Task.FromResult(Array.Empty<byte>());
    }
}

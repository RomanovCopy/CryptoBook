using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public sealed class RichTextFileTemplate:IFileTemplate
    {
        public string Id => "rtf";
        public string DisplayName =>"Rich Text Format";
        public string DefaultExtension => ".rtf";
        public string SuggestedBaseName => "New document";
        public Encoding? DefaultEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); // UTF-8 BOM
        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
            => Task.FromResult(System.Text.Encoding.UTF8.GetBytes(@"{\rtf1\ansi\deff0 {\fonttbl {\f0 Arial;}} \f0\fs24 New document \par }"));
    }
}

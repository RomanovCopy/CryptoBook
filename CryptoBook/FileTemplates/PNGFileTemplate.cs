using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public class PNGFileTemplate: IFileTemplate
    {
        public string Id => "png";
        public string DisplayName => "PNG";
        public string DefaultExtension => ".png";
        public string SuggestedBaseName => "NewImage";
        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
            => Task.FromResult(Array.Empty<byte>());
    }
}

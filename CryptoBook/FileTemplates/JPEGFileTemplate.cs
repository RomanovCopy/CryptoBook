using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public class JPEGFileTemplate:IFileTemplate
    {
        public string Id => "jpeg";

        public string DisplayName => "JPEG";

        public string DefaultExtension => ".jpeg";

        public string SuggestedBaseName => "NewImage";

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
            => Task.FromResult(Array.Empty<byte>());
    }
}

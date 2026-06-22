using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public class JPGFileTemplate: IFileTemplate
    {
        public string Id => "jpg";
        public string DisplayName => "JPG";
        public string DefaultExtension => ".jpg";
        public string SuggestedBaseName => "NewImage";
        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
            => Task.FromResult(Array.Empty<byte>());
    }
}

using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public sealed class JsonFileTemplate: IFileTemplate
    {
        public string Id => "json";
        public string DisplayName => "JSON";
        public string DefaultExtension => ".json";
        public string SuggestedBaseName => "new";
        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
            => Task.FromResult(System.Text.Encoding.UTF8.GetBytes("{\n}\n"));
    }
}

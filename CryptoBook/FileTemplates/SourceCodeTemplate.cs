using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public sealed class SourceCodeTemplate:IFileTemplate
    {
        public string Id => "cs";
        public string DisplayName => "C#";
        public string DefaultExtension => ".cs";
        public string SuggestedBaseName => "NewClass";

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
            => Task.FromResult(System.Text.Encoding.UTF8.GetBytes(
@"using System;

namespace MyNamespace
{
    public class NewClass
    {
    }
}
"));
    }
}

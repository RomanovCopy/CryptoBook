using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public class SecureFileTemplate:IFileTemplate
    {
        public string Id => "Encrypted file";

        public string DisplayName => "Зашифрованный файл";

        public string DefaultExtension => ".cbox";

        public IReadOnlyCollection<string> Extensions =>
        [
            ".cbox",
        ];


        public string SuggestedBaseName => "New Encrypted File";

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct) => Task.FromResult(Array.Empty<byte>());
    }
}

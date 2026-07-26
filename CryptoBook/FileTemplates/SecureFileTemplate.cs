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

        // Актуальное расширение защищённых файлов; .cbox оставлено для совместимости.
        public string DefaultExtension => ".cbook";

        public IReadOnlyCollection<string> Extensions =>
        [
            ".cbook",
            ".cbox",
        ];


        public string SuggestedBaseName => "New Encrypted File";

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct) => Task.FromResult(Array.Empty<byte>());
    }
}

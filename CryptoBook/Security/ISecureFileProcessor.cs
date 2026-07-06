using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    public interface ISecureFileProcessor
    {
        public Task EncryptFileAsync(string inputFile, string outputFile, char[] password, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

        public Task DecryptFileAsync(string inputFile, string outputFile, char[] password, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

        public Task<Stream> DecryptFileAsync(string inputFile, char[] password, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
    }
}

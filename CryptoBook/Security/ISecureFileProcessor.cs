using CryptoBook.Interfaces;

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
        public Task EncryptFileAsync(string inputFile, string outputFile, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);

        public Task DecryptFileAsyncToFile(string inputFile, string outputFile, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);

        public Task<Stream> DecryptFileAsyncToStream(string inputFile, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
    }
}

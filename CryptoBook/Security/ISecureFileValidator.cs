using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    public interface ISecureFileValidator
    {
        public Task<SecureFileState> GetStateAsync(string filePath, string password, CancellationToken cancellationToken = default);
        public Task<bool> IsEncryptedAsync(string filePath, string password, CancellationToken cancellationToken = default);
        public Task<bool> HasCryptoBookHeaderAsync(string filePath, CancellationToken cancellationToken = default);

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    public interface ISecureFileValidator
    {
        public Task<bool> HasCryptoBookHeaderAsync(string filePath, CancellationToken cancellationToken = default);
    }
}

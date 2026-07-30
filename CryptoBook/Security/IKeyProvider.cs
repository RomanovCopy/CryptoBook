using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    public interface IKeyProvider : IService
    {
        bool HasKey { get; }

        void SetKey(ReadOnlySpan<char> password);

        byte[] DeriveKey(byte[] salt);

        Task<byte[]> DeriveKeyAsync(
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default);

        void Clear();
    }
}

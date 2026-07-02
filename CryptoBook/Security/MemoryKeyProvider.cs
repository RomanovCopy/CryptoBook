using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    internal sealed class MemoryKeyProvider: IKeyProvider, IDisposable
    {
        private byte[]? _passwordBytes;

        public bool HasKey => _passwordBytes is { Length: > 0 };

        public void SetKey(ReadOnlySpan<char> password)
        {
            Clear();

            int byteCount = Encoding.UTF8.GetByteCount(password);
            _passwordBytes = new byte[byteCount];

            Encoding.UTF8.GetBytes(password, _passwordBytes);
        }

        public byte[] DeriveKey(byte[] salt)
        {
            if(_passwordBytes is null)
                throw new InvalidOperationException("Ключ не задан.");

            using Rfc2898DeriveBytes rfc2898 = new( _passwordBytes, salt, 100_000, HashAlgorithmName.SHA256);

            return rfc2898.GetBytes(32);
        }

        public void Clear()
        {
            if(_passwordBytes is null)
                return;

            CryptographicOperations.ZeroMemory(_passwordBytes);

            _passwordBytes = null;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}

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
        private readonly IPasswordKeyDeriver _keyDeriver;
        private byte[]? _passwordBytes;

        public MemoryKeyProvider(IPasswordKeyDeriver keyDeriver)
        {
            _keyDeriver = keyDeriver ?? throw new ArgumentNullException(nameof(keyDeriver));
        }

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

            return Rfc2898DeriveBytes.Pbkdf2(
                _passwordBytes,
                salt,
                100_000,
                HashAlgorithmName.SHA256,
                32);
        }

        public Task<byte[]> DeriveKeyAsync(
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default)
        {
            if(_passwordBytes is null)
                throw new InvalidOperationException("Ключ не задан.");

            return _keyDeriver.DeriveAsync(
                _passwordBytes,
                salt,
                parameters,
                cancellationToken);
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

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

        bool CanEncrypt { get; }

        Task<byte[]> DeriveEncryptionKeyAsync(ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters, CancellationToken cancellationToken = default)
        {
            if(!CanEncrypt)
                throw new System.Security.Cryptography.CryptographicException(
                    CryptoBook.Infrastructure.LocalizationManager.GetString("Key.StrongPasswordRequired"));
            return DeriveKeyAsync(salt, parameters, cancellationToken);
        }

        void SetKey(ReadOnlySpan<char> password);

        byte[] DeriveKey(byte[] salt);

        Task<byte[]> DeriveKeyAsync(
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default);

        void Clear();
    }
}

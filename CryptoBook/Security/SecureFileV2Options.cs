using System.Security.Cryptography;

namespace CryptoBook.Security
{
    public sealed class SecureFileV2Options
    {
        public KeyDerivationParameters KeyDerivation { get; init; } =
            KeyDerivationParameters.SecureFileV2;

        public int ChunkSize { get; init; } = SecureFileFormat.V2ChunkSize;

        public void Validate()
        {
            KeyDerivation.Validate();
            if(ChunkSize is < 4096 or > SecureFileFormat.V2ChunkSize)
                throw new CryptographicException("Недопустимый размер блока шифрования.");
        }
    }
}

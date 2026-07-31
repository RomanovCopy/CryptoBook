using System.Security.Cryptography;

namespace CryptoBook.Security
{
    public sealed record KeyDerivationParameters(
        int Iterations,
        int MemorySizeKiB,
        int DegreeOfParallelism,
        int OutputLength)
    {
        public static KeyDerivationParameters SecureFileV2 { get; } = new(
            Iterations: 3,
            MemorySizeKiB: 64 * 1024,
            DegreeOfParallelism: 4,
            OutputLength: 32);

        public void Validate()
        {
            if(Iterations is < 1 or > 6)
                throw new CryptographicException("Недопустимое число проходов Argon2id.");
            if(MemorySizeKiB is < 8 * 1024 or > 256 * 1024)
                throw new CryptographicException("Недопустимый объём памяти Argon2id.");
            if(DegreeOfParallelism is < 1 or > 8)
                throw new CryptographicException("Недопустимый параллелизм Argon2id.");
            if(OutputLength is < 16 or > 64)
                throw new CryptographicException("Недопустимая длина ключа.");
        }
    }
}

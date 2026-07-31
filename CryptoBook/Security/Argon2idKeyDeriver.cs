using Konscious.Security.Cryptography;

using System.Security.Cryptography;

namespace CryptoBook.Security
{
    public sealed class Argon2idKeyDeriver: IPasswordKeyDeriver
    {
        public async Task<byte[]> DeriveAsync(
            ReadOnlyMemory<byte> password,
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default)
        {
            parameters.Validate();
            cancellationToken.ThrowIfCancellationRequested();

            byte[] passwordCopy = password.ToArray();
            byte[] saltCopy = salt.ToArray();
            byte[]? derivedKey = null;

            try
            {
                using Argon2id argon2 = new(passwordCopy)
                {
                    Salt = saltCopy,
                    Iterations = parameters.Iterations,
                    MemorySize = parameters.MemorySizeKiB,
                    DegreeOfParallelism = parameters.DegreeOfParallelism
                };

                derivedKey = await argon2.GetBytesAsync(parameters.OutputLength);
                cancellationToken.ThrowIfCancellationRequested();
                byte[] result = derivedKey;
                derivedKey = null;
                return result;
            } finally
            {
                if(derivedKey is not null)
                    CryptographicOperations.ZeroMemory(derivedKey);
                CryptographicOperations.ZeroMemory(passwordCopy);
                CryptographicOperations.ZeroMemory(saltCopy);
            }
        }
    }
}

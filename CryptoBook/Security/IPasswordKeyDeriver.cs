namespace CryptoBook.Security
{
    public interface IPasswordKeyDeriver
    {
        Task<byte[]> DeriveAsync(
            ReadOnlyMemory<byte> password,
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default);
    }
}

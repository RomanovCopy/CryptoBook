namespace CryptoBook.Security
{
    public sealed record DecryptionOptions(
        EncryptionTargetMode TargetMode,
        DecryptionOutputFormat OutputFormat);
}

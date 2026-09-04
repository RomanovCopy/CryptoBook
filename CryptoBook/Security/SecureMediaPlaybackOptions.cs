namespace CryptoBook.Security;

public sealed class SecureMediaPlaybackOptions
{
    public const long DefaultLegacyMemoryLimitBytes = 256L * 1024 * 1024;

    public long LegacyMemoryLimitBytes { get; init; } =
        DefaultLegacyMemoryLimitBytes;

    public void Validate()
    {
        if(LegacyMemoryLimitBytes is <= 0 or > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(LegacyMemoryLimitBytes));
    }
}

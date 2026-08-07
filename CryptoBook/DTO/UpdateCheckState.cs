namespace CryptoBook.DTO
{
    public sealed record UpdateCheckState(
        DateTimeOffset? LastCheckUtc,
        string? SkippedVersion)
    {
        public static UpdateCheckState Empty { get; } = new(null, null);
    }
}

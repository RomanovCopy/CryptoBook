namespace CryptoBook.DTO
{
    public sealed record ApplicationRelease(
        SemanticVersion Version,
        string Name,
        string Notes,
        Uri ReleaseUri,
        DateTimeOffset? PublishedAt)
    {
        /// <summary>Direct download URL of the Windows Inno Setup package.</summary>
        public Uri? InstallerUri { get; init; }
    }
}

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

        /// <summary>SHA-256 manifest published with the release.</summary>
        public Uri? Sha256ChecksumsUri { get; init; }

        /// <summary>Declaration stating whether the release uses Authenticode.</summary>
        public Uri? SigningStatusUri { get; init; }
    }
}

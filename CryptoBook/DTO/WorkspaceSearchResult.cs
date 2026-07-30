namespace CryptoBook.DTO
{
    public sealed record WorkspaceSearchResult(
        string Name,
        string FullPath,
        string RelativePath);

    public sealed record WorkspaceSearchOutcome(
        IReadOnlyList<WorkspaceSearchResult> Results,
        bool IsTruncated,
        int SkippedDirectoryCount);
}

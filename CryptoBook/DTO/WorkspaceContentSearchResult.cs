namespace CryptoBook.DTO
{
    public sealed record WorkspaceContentSearchResult(
        string Name,
        string FullPath,
        string RelativePath,
        string Snippet,
        int MatchCount,
        bool IsEncrypted);

    public sealed record WorkspaceContentSearchOutcome(
        IReadOnlyList<WorkspaceContentSearchResult> Results,
        bool IsTruncated,
        int SkippedDirectoryCount,
        int SkippedFileCount,
        int SkippedEncryptedFileCount);

    public sealed record WorkspaceContentSearchProgress(
        int ProcessedFileCount,
        string CurrentRelativePath);

    public sealed record WorkspaceDocumentDeleteResult(
        bool Deleted,
        bool Cancelled,
        string? Error)
    {
        public static WorkspaceDocumentDeleteResult Success() =>
            new(true, false, null);
        public static WorkspaceDocumentDeleteResult Cancel() =>
            new(false, true, null);
        public static WorkspaceDocumentDeleteResult Fail(string? error) =>
            new(false, false, error);
    }
}

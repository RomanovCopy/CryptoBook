namespace CryptoBook.Services
{
    public sealed class WorkspaceContentSearchOptions
    {
        public int MaxResults { get; init; } = 200;
        public long MaxFileSizeBytes { get; init; } = 16 * 1024 * 1024;
        public int SnippetLength { get; init; } = 180;
    }
}

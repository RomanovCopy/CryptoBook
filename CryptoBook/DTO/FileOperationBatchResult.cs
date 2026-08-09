namespace CryptoBook.DTO
{
    public sealed class FileOperationBatchResult
    {
        public FileOperationBatchResult(
            IReadOnlyList<FileOperationResult> results,
            int completedCount,
            int skippedCount,
            bool canceled,
            bool hasPartialChanges)
        {
            Results = results;
            CompletedCount = completedCount;
            SkippedCount = skippedCount;
            Canceled = canceled;
            HasPartialChanges = hasPartialChanges;
        }

        public IReadOnlyList<FileOperationResult> Results { get; }
        public int CompletedCount { get; }
        public int SkippedCount { get; }
        public bool Canceled { get; }
        public bool HasPartialChanges { get; }
        public bool Success => !Canceled && Results.All(result => result.Success);
        public FileOperationResult? Failure => Results.FirstOrDefault(result => !result.Success);
    }
}

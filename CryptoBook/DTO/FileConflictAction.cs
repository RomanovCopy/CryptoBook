namespace CryptoBook.DTO
{
    public enum FileConflictAction
    {
        Replace,
        Skip,
        KeepBoth,
        Cancel
    }

    public sealed record FileConflictDecision(
        FileConflictAction Action,
        bool ApplyToAll = false);
}

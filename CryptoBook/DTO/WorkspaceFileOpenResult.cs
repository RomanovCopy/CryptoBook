namespace CryptoBook.DTO
{
    public readonly record struct WorkspaceFileOpenResult(
        bool Success,
        bool OpenedInternally,
        bool Cancelled = false,
        string? Error = null)
    {
        public static WorkspaceFileOpenResult InternalSuccess() =>
            new(true, true);

        public static WorkspaceFileOpenResult ExternalSuccess() =>
            new(true, false);

        public static WorkspaceFileOpenResult Cancel() =>
            new(false, false, true);

        public static WorkspaceFileOpenResult Fail(string? error) =>
            new(false, false, false, error);
    }
}

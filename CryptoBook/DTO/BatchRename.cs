namespace CryptoBook.DTO;

public enum RenameCase { Unchanged, Lower, Upper, Title }

public sealed record BatchRenameOptions
{
    public string NameMask { get; init; } = "[N]";
    public string ExtensionMask { get; init; } = "[E]";
    public string Search { get; init; } = string.Empty;
    public string Replacement { get; init; } = string.Empty;
    public bool UseRegex { get; init; }
    public bool MatchCase { get; init; }
    public RenameCase Case { get; init; }
    public long CounterStart { get; init; } = 1;
    public long CounterStep { get; init; } = 1;
    public int CounterDigits { get; init; } = 3;
}

public sealed record BatchRenameEntry(string SourcePath, string NewName, string? Error = null)
{
    public string OldName => System.IO.Path.GetFileName(SourcePath);
    public string TargetPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(SourcePath)!, NewName);
    public bool IsChanged => !string.Equals(OldName, NewName, StringComparison.Ordinal);
}

public sealed record BatchRenameResult(
    bool Success,
    bool Cancelled,
    string? Error,
    IReadOnlyDictionary<string, string> ChangedPaths);

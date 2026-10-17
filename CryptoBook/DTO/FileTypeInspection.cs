namespace CryptoBook.DTO;

public sealed record FileTypeInspection(
    string DetectedType,
    string Header,
    bool HasWarning,
    IReadOnlyList<string> Messages);

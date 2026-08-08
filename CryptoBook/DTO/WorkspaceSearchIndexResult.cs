namespace CryptoBook.DTO;

public sealed record WorkspaceIndexedDocument(
    string Name,
    string Path,
    string RelativePath,
    string Body);

public sealed record WorkspaceSearchIndexUpdateOutcome(
    IReadOnlyList<string> EncryptedFiles,
    int SkippedDirectoryCount,
    int SkippedFileCount);

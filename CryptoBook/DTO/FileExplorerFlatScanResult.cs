using CryptoBook.Interfaces;

namespace CryptoBook.DTO;

public sealed record FileExplorerFlatScanResult(
    IReadOnlyList<IFileItem> Files,
    int SkippedDirectoryCount);

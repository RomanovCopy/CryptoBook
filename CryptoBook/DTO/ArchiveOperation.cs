namespace CryptoBook.DTO;

public enum ArchiveAction { Create, Extract, Test }
public enum ArchiveFormat { Zip, SevenZip }

public sealed record ArchiveOperationResult(string OutputPath, int FileCount, long TotalBytes);

public sealed record ArchiveExtractionLimits(long MaximumBytes = 20L * 1024 * 1024 * 1024,
    int MaximumEntries = 100_000);

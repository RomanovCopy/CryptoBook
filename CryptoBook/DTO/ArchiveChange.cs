namespace CryptoBook.DTO;

public enum ArchiveChangeKind { Put, Delete, Rename, CreateDirectory }

/// <summary>Paths inside an archive are relative Windows paths. Put accepts a local source.</summary>
public sealed record ArchiveChange(ArchiveChangeKind Kind, string Path, string? Source = null,
    bool Overwrite = false);

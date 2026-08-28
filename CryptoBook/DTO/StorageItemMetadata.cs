namespace CryptoBook.DTO;

public enum StorageItemKind
{
    File,
    Container,
    Root
}

[Flags]
public enum StorageProviderCapabilities
{
    None = 0,
    Browse = 1 << 0,
    Read = 1 << 1,
    Write = 1 << 2,
    CreateContainer = 1 << 3,
    Rename = 1 << 4,
    Delete = 1 << 5,
    CopyWithinProvider = 1 << 6,
    MoveWithinProvider = 1 << 7,
    RawStreams = 1 << 8,
    Monitor = 1 << 9,
    Search = 1 << 10,
    Preview = 1 << 11,
    OpenExternally = 1 << 12,
    Encrypt = 1 << 13
}

public sealed record StorageItemMetadata(
    StorageLocation Location,
    string Name,
    StorageItemKind Kind,
    long Size = 0,
    DateTime? LastWriteTimeUtc = null,
    bool IsHidden = false,
    bool IsReadOnly = false,
    StorageProviderCapabilities Capabilities = StorageProviderCapabilities.None,
    string? DisplayPath = null,
    string? StatusText = null)
{
    public bool IsContainer => Kind is StorageItemKind.Container or StorageItemKind.Root;
}

public sealed record StorageDeletionEntry(StorageLocation Location, long Size);

using CryptoBook.Interfaces;

namespace CryptoBook.DTO;

public sealed record FileDetailsTarget(
    string Name,
    string Path,
    string DisplayPath,
    long Size,
    DateTime LastWriteTimeUtc,
    bool IsDirectory)
{
    public static FileDetailsTarget FromItem(ISystemItem item) => new(
        item.Name, item.FullPath, item.DisplayPath, item.Size,
        item.LastWriteTimeUtc, item is IContainerSystemItem);

    public string? LocalPath => StorageLocation.TryParse(Path, out var location) &&
        location.IsLocal ? location.OpaqueId : null;
}

public sealed record FilePropertyEntry(
    string Category, string Name, string Value, string Key);

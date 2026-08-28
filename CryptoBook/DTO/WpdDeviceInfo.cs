namespace CryptoBook.DTO;

/// <summary>
/// A Windows Portable Device exposed through the system MTP stack.
/// </summary>
public sealed record WpdDeviceInfo(
    string Id,
    string DisplayName,
    bool IsAvailable = true,
    string StatusText = "online");

/// <summary>
/// Provider-neutral metadata returned by the WPD transport. RelativePath is
/// interpreted only by the WPD provider and never by explorer consumers.
/// </summary>
public sealed record WpdStorageEntry(
    string RelativePath,
    string Name,
    bool IsContainer,
    long Size,
    DateTime? LastWriteTimeUtc,
    bool IsHidden = false,
    bool IsReadOnly = false);

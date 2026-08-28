namespace CryptoBook.DTO;

public enum AndroidDeviceState
{
    Online,
    Offline,
    Unauthorized,
    Unknown
}

public sealed record AndroidDeviceInfo(
    string Serial,
    string DisplayName,
    AndroidDeviceState State,
    string? Product = null,
    string? Model = null,
    string? TransportId = null);

public sealed record AndroidRemoteEntry(
    string Path,
    string Name,
    bool IsContainer,
    long Size,
    DateTime? LastWriteTimeUtc,
    bool IsHidden = false);

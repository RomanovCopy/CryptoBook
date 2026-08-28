using System.Text;

using CryptoBook.DTO;

namespace CryptoBook.Services;

internal static class WpdLocatorCodec
{
    public const string ProviderId = "mtp";
    public const string RootPath = "/";

    public static StorageLocation Encode(string deviceId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return new StorageLocation(
            ProviderId,
            EncodePart(deviceId) + "." + EncodePart(NormalizePath(relativePath)));
    }

    public static (string DeviceId, string RelativePath) Decode(StorageLocation location)
    {
        if(!location.ProviderId.Equals(ProviderId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Not an MTP storage locator.", nameof(location));

        int separator = location.OpaqueId.IndexOf('.');
        if(separator <= 0 || separator == location.OpaqueId.Length - 1)
            throw new FormatException("Invalid MTP storage locator.");

        return (
            DecodePart(location.OpaqueId[..separator]),
            NormalizePath(DecodePart(location.OpaqueId[(separator + 1)..])));
    }

    public static string NormalizePath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string[] segments = value.Replace('\\', '/').Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);
        if(segments.Any(segment => segment is "." or ".."))
            throw new FormatException("MTP locator contains an invalid path segment.");
        return segments.Length == 0 ? RootPath : "/" + string.Join('/', segments);
    }

    private static string EncodePart(string value) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static string DecodePart(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}

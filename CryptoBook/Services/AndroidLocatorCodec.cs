using System.Text;

using CryptoBook.DTO;

namespace CryptoBook.Services;

internal static class AndroidLocatorCodec
{
    public const string ProviderId = "android";

    public static StorageLocation Encode(string serial, string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        return new StorageLocation(
            ProviderId,
            EncodePart(serial) + "." + EncodePart(objectId));
    }

    public static (string Serial, string ObjectId) Decode(StorageLocation location)
    {
        if(!location.ProviderId.Equals(ProviderId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Not an Android storage locator.", nameof(location));
        int separator = location.OpaqueId.IndexOf('.');
        if(separator <= 0 || separator == location.OpaqueId.Length - 1)
            throw new FormatException("Invalid Android storage locator.");
        return (
            DecodePart(location.OpaqueId[..separator]),
            DecodePart(location.OpaqueId[(separator + 1)..]));
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

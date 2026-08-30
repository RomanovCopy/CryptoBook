using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Text.RegularExpressions;

namespace CryptoBook.Services;

/// <summary>
/// Keeps provider locators operationally opaque while replacing them with
/// provider-owned display paths at the UI boundary.
/// </summary>
public sealed class StoragePathDisplayService
{
    private static readonly Regex RemoteLocatorPattern = new(
        @"(?:android|mtp)://[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    private readonly IStorageFacade _storage;

    public StoragePathDisplayService(IStorageFacade storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public string FormatPath(string path)
    {
        if(string.IsNullOrWhiteSpace(path))
            return path;

        try
        {
            StorageLocation location = _storage.Resolve(path);
            return _storage.FormatDisplayPath(location);
        }
        catch(ArgumentException)
        {
            return path;
        }
        catch(NotSupportedException)
        {
            return path;
        }
        catch(FormatException)
        {
            return path;
        }
    }

    public string FormatMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return RemoteLocatorPattern.Replace(
            message,
            match => FormatPath(match.Value));
    }
}

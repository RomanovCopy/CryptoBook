namespace CryptoBook.DTO;

/// <summary>
/// Provider-qualified, opaque identifier of a storage object.
/// Consumers must never interpret <see cref="OpaqueId"/> as a file-system path.
/// </summary>
public readonly record struct StorageLocation
{
    public const string LocalProviderId = "local";

    public StorageLocation(string providerId, string opaqueId)
    {
        if(string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider id is empty.", nameof(providerId));
        if(string.IsNullOrWhiteSpace(opaqueId))
            throw new ArgumentException("Storage object id is empty.", nameof(opaqueId));

        ProviderId = providerId.Trim().ToLowerInvariant();
        OpaqueId = opaqueId;
    }

    public string ProviderId { get; }
    public string OpaqueId { get; }

    public bool IsLocal =>
        ProviderId.Equals(LocalProviderId, StringComparison.OrdinalIgnoreCase);

    public static StorageLocation Parse(string value)
    {
        if(string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Storage location is empty.", nameof(value));

        int separator = value.IndexOf("://", StringComparison.Ordinal);
        return separator > 0
            ? new StorageLocation(value[..separator], value[(separator + 3)..])
            : new StorageLocation(LocalProviderId, value);
    }

    public static bool TryParse(string? value, out StorageLocation location)
    {
        try
        {
            location = Parse(value!);
            return true;
        }
        catch(ArgumentException)
        {
            location = default;
            return false;
        }
    }

    public override string ToString() => IsLocal
        ? OpaqueId
        : $"{ProviderId}://{OpaqueId}";
}

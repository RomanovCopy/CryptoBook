namespace CryptoBook.DTO;

public sealed record RecycleBinVolumeSpace(
    string VolumePath, long RequiredBytes, long? CapacityBytes, long UsedBytes)
{
    public long? AvailableBytes => CapacityBytes is { } capacity ? Math.Max(0, capacity - UsedBytes) : null;
    public bool Fits => AvailableBytes is { } available && RequiredBytes <= available && CapacityBytes > 0;
}

public sealed record RecycleBinDeletionPlan(IReadOnlyList<RecycleBinVolumeSpace> Volumes)
{
    public bool IsKnown => Volumes.All(volume => volume.CapacityBytes is not null);
    public bool Fits => Volumes.All(volume => volume.Fits);
}

using CryptoBook.Interfaces;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace CryptoBook.Infrastructure;

/// <summary>Packages both documents inside the existing encrypted snapshot envelope.</summary>
internal static class WorkspaceSnapshotPayload
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CBWORK01");

    public static async Task WriteAsync(Stream destination, Stream active,
        WorkspaceDocumentSnapshot? inactive, CancellationToken cancellationToken = default)
    {
        active.Position = 0;
        if(inactive is null)
        {
            await active.CopyToAsync(destination, cancellationToken);
            return;
        }
        await destination.WriteAsync(Magic, cancellationToken);
        using var buffer = new MemoryStream();
        using(var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using(var entry = archive.CreateEntry("active", CompressionLevel.NoCompression).Open())
                await active.CopyToAsync(entry, cancellationToken);
            await using(var entry = archive.CreateEntry("inactive", CompressionLevel.NoCompression).Open())
                await JsonSerializer.SerializeAsync(entry, inactive, cancellationToken: cancellationToken);
        }
        buffer.Position = 0;
        await buffer.CopyToAsync(destination, cancellationToken);
    }

    public static async Task<(MemoryStream Active, WorkspaceDocumentSnapshot? Inactive)> ReadAsync(
        Stream source, CancellationToken cancellationToken = default)
    {
        var active = new MemoryStream();
        try
        {
            long start = source.Position;
            byte[] prefix = new byte[Magic.Length];
            int read = await source.ReadAtLeastAsync(prefix, prefix.Length,
                throwOnEndOfStream: false, cancellationToken);
            if(read != prefix.Length || !prefix.AsSpan().SequenceEqual(Magic))
            {
                source.Position = start;
                await source.CopyToAsync(active, cancellationToken);
                active.Position = 0;
                return (active, null);
            }
            // ZIP offsets are relative to the beginning of the archive payload.
            using Stream payload = BinarySnapshotEnvelope.OpenPayloadStream(source);
            using var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: true);
            await using(var entry = (archive.GetEntry("active") ??
                throw new InvalidDataException("Active document is missing.")).Open())
                await entry.CopyToAsync(active, cancellationToken);
            await using var inactiveEntry = (archive.GetEntry("inactive") ??
                throw new InvalidDataException("Inactive document is missing.")).Open();
            var inactive = await JsonSerializer.DeserializeAsync<WorkspaceDocumentSnapshot>(
                inactiveEntry, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("Inactive document is invalid.");
            active.Position = 0;
            return (active, inactive);
        }
        catch
        {
            active.Dispose();
            throw;
        }
    }
}

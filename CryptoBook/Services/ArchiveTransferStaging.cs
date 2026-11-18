using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

/// <summary>Bound remote data before it can occupy the local disk.</summary>
internal sealed class ArchiveTransferStaging(IStorageFacade storage, IProgressReporter? progress, CancellationToken token)
{
    private long remaining = new ArchiveExtractionLimits().MaximumBytes;
    private int entries;

    internal async Task CopyAsync(StorageLocation source, string destination, int depth = 0)
    {
        token.ThrowIfCancellationRequested();
        if(++entries > 100_000 || depth > 128) throw new IOException(ArchivePathPolicy.Text("TooManyEntries"));
        var provider = storage.GetProvider(source);
        var metadata = await provider.GetMetadataAsync(source, token) ?? throw new FileNotFoundException(source.ToString());
        ArchivePathPolicy.EnsureNoLinks(destination);
        if(metadata.IsContainer)
        {
            Directory.CreateDirectory(destination);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var child in await provider.GetChildrenAsync(source, true, token))
            {
                string name = ArchivePathPolicy.ValidateEntry(child.Name);
                if(name.Contains('\\') || !names.Add(name)) throw new InvalidDataException(ArchivePathPolicy.Text("DuplicatePath"));
                await CopyAsync(child.Location, Path.Combine(destination, name), depth + 1);
            }
            return;
        }
        if(metadata.Size < 0 || metadata.Size > remaining) throw new IOException(ArchivePathPolicy.Text("ExtractionLimit"));
        remaining -= metadata.Size;
        await using var input = await provider.OpenRawReadAsync(source, token);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        byte[] buffer = new byte[81920];
        long written = 0;
        int count;
        while((count = await input.ReadAsync(buffer, token)) != 0)
        {
            if(count > metadata.Size - written) throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
            if(new DriveInfo(Path.GetPathRoot(destination)!).AvailableFreeSpace < count + 16L * 1024 * 1024)
                throw new IOException(ArchivePathPolicy.Text("NotEnoughSpace"));
            await output.WriteAsync(buffer.AsMemory(0, count), token);
            written += count;
            progress?.Report(metadata.Size == 0 ? 1 : written / (double)metadata.Size, metadata.Name);
        }
        if(written != metadata.Size) throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
    }
}

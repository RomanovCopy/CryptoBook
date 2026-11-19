using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

public sealed partial class ArchiveStorageProvider
{
    internal async Task<StorageLocation> MountSourceAsync(Guid owner, StorageLocation source, IStorageFacade storage,
        string? password, CancellationToken token)
    {
        if(source.IsLocal) return await MountAsync(owner, source.OpaqueId, password, token);
        string display = storage.FormatDisplayPath(source);
        if(display.Split("!/", StringSplitOptions.None).Length > 5)
            throw new IOException(ArchivePathPolicy.Text("NestingLimit"));
        var metadata = await storage.GetMetadataAsync(source, token) ?? throw new FileNotFoundException(display);
        if(metadata.IsContainer) throw new IOException(ArchivePathPolicy.Text("NotAFile"));
        // A remote/nested volume requires its siblings; do not mistake a partial snapshot for a complete set.
        if(ArchivePathPolicy.IsVolume(metadata.Name)) throw new NotSupportedException(ArchivePathPolicy.Text("VolumesLocalOnly"));
        long maximum = new ArchiveExtractionLimits().MaximumBytes;
        long retainedBytes = mounts.Values.Where(mount => mount.Owner == owner && mount.Workspace is not null)
            .Sum(mount => new FileInfo(mount.Session.ArchivePath).Length);
        if(metadata.Size > maximum - retainedBytes) throw new IOException(ArchivePathPolicy.Text("ExtractionLimit"));
        string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CryptoBook", "ArchiveWork");
        string workspace = ArchiveWorkspace.CreateTransient("archive-snapshot");
        try
        {
            string name = ArchivePathPolicy.ValidateEntry(metadata.Name);
            if(name.Contains('\\')) throw new InvalidDataException(ArchivePathPolicy.Text("UnsafePath"));
            string path = Path.Combine(workspace, name);
            if(new DriveInfo(Path.GetPathRoot(parent)!).AvailableFreeSpace < metadata.Size + 16L * 1024 * 1024)
                throw new IOException(ArchivePathPolicy.Text("NotEnoughSpace"));
            await using(var input = await storage.GetProvider(source).OpenRawReadAsync(source, token))
            await using(var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long written = 0;
                int count;
                while((count = await input.ReadAsync(buffer, token)) != 0)
                {
                    if(count > metadata.Size - written || count > maximum - retainedBytes - written)
                        throw new IOException(ArchivePathPolicy.Text("ExtractionLimit"));
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    written += count;
                }
                if(written != metadata.Size) throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
            }
            var root = await MountAsync(owner, path, password, token);
            var mount = Resolve(root).Mount;
            mount.Workspace = workspace;
            mount.OriginalLocation = source.ToString();
            mount.DisplayPath = display;
            mount.Parent = storage.GetParent(source);
            return root;
        }
        catch { await ArchiveService.RemoveWorkspaceAsync(workspace); throw; }
    }
}

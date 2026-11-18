using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Security.Cryptography;

namespace CryptoBook.Services;

public sealed partial class ArchiveStorageProvider
{
    private readonly SemaphoreSlim updateGate = new(1, 1);

    internal bool CanEdit(StorageLocation location) => Resolve(location).Mount.CanEdit;
    internal string? PersistentDirectory(StorageLocation location)
    {
        var mount = Resolve(location).Mount;
        if(mount.Parent is StorageLocation parent)
            return IsArchive(parent) ? PersistentDirectory(parent) : parent.IsLocal ? parent.OpaqueId : null;
        return Path.GetDirectoryName(mount.Session.ArchivePath);
    }
    internal void CloseDocument(Guid owner, StorageLocation location)
    {
        var value = Resolve(location);
        if(value.Mount.Owner == owner && mounts.TryRemove(value.Id, out var mount)) mount.Session.Dispose();
    }
    internal async Task<StorageLocation> RetainDocumentAsync(Guid owner, StorageLocation location, CancellationToken token)
    {
        var value = Resolve(location);
        if(!value.Mount.CanEdit) return location;
        var root = await MountAsync(owner, value.Mount.Session.ArchivePath, value.Mount.Session.GetPassword(), token);
        return Location(Resolve(root).Id, value.Relative);
    }

    internal Task ReplaceDocumentAsync(StorageLocation location, string source, string hash,
        IProgressReporter progress, CancellationToken token) => UpdateMountAsync(location,
            [new(ArchiveChangeKind.Put, Resolve(location).Relative, source, Overwrite: true)], hash, progress, token);
    internal async Task<string> GetSourceHashAsync(StorageLocation location, CancellationToken token)
    {
        using var input = File.OpenRead(Resolve(location).Mount.Session.ArchivePath);
        return Convert.ToHexString(await SHA256.HashDataAsync(input, token));
    }

    private async Task<FileOperationResult> ChangeAsync(StorageLocation location,
        Func<string, ArchiveChange> change, IProgressReporter? progress, CancellationToken token)
    {
        try
        {
            await UpdateMountAsync(location, [change(Resolve(location).Relative)], null, progress, token);
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException) { throw; }
        catch(Exception exception) { return FileOperationResult.Fail(exception.Message); }
    }

    internal async Task UpdateMountAsync(StorageLocation location, IReadOnlyList<ArchiveChange> changes,
        string? expectedHash, IProgressReporter? progress, CancellationToken token)
    {
        await updateGate.WaitAsync(token);
        try
        {
            var value = Resolve(location);
            if(!value.Mount.CanEdit || changes.Any(change => change.Path.Length == 0)) throw ReadOnlyError();
            string archive = value.Mount.Session.ArchivePath;
            var affected = mounts.Where(pair => pair.Value.Session.ArchivePath.Equals(archive,
                StringComparison.OrdinalIgnoreCase)).Select(pair => (pair.Key, Mount: pair.Value,
                    Password: pair.Value.Session.GetPassword())).ToArray();
            string? password = value.Mount.Session.GetPassword();
            foreach(var item in affected) item.Mount.Session.Dispose();
            try { await service.UpdateAsync(archive, changes, password, progress: progress, cancellationToken: token, expectedHash: expectedHash); }
            finally
            {
                // Reopen after failure as well. A failed commit must leave the original usable.
                foreach(var item in affected)
                {
                    if(!mounts.ContainsKey(item.Key)) continue;
                    try
                    {
                        var session = await service.OpenReadSessionAsync(archive, item.Password, CancellationToken.None);
                        if(!mounts.TryGetValue(item.Key, out var current) || !ReferenceEquals(current, item.Mount))
                        {
                            session.Dispose();
                            continue;
                        }
                        item.Mount.Nodes = BuildNodes(session);
                        item.Mount.Session = session;
                        if(!mounts.ContainsKey(item.Key)) session.Dispose();
                    }
                    catch
                    {
                        // A refresh failure cannot turn an already committed save into a reported failure.
                        mounts.TryRemove(item.Key, out _);
                    }
                }
            }
        }
        finally { updateGate.Release(); }
    }

    private static Dictionary<string, ArchiveService.Entry> BuildNodes(ArchiveReadSession session)
    {
        var nodes = new Dictionary<string, ArchiveService.Entry>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in session.Entries)
        {
            nodes[entry.RelativePath] = entry;
            for(string? parent = Path.GetDirectoryName(entry.RelativePath); !string.IsNullOrEmpty(parent);
                parent = Path.GetDirectoryName(parent))
                nodes.TryAdd(parent, new ArchiveService.Entry(parent, parent, true, 0, null));
        }
        return nodes;
    }

    private static void DeleteSnapshot(string path)
    {
        ArchivePathPolicy.EnsureNoLinks(path);
        ArchiveService.RemoveWorkspaceAsync(path).GetAwaiter().GetResult();
    }
}

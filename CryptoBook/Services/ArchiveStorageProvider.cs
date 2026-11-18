using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Collections.Concurrent;
using System.IO;

namespace CryptoBook.Services;

/// <summary>Explorer archive sessions belong to their window; they never appear as shared device roots.</summary>
public sealed partial class ArchiveStorageProvider: IStorageProvider, IFileProviderService, ILocalTransferProvider
{
    public const string ProviderId = "archive";
    private readonly ArchiveService service;
    private readonly ISystemItemCreateService items;
    private readonly ConcurrentDictionary<string, Mount> mounts = new(StringComparer.Ordinal);

    public ArchiveStorageProvider(ArchiveService service, ISystemItemCreateService items)
    {
        this.service = service;
        this.items = items;
    }

    public string Id => ProviderId;
    public string Scheme => Id;
    public StorageProviderCapabilities Capabilities => StorageProviderCapabilities.Browse |
        StorageProviderCapabilities.Read | StorageProviderCapabilities.RawStreams | StorageProviderCapabilities.Preview |
        StorageProviderCapabilities.Write | StorageProviderCapabilities.Delete | StorageProviderCapabilities.Rename |
        StorageProviderCapabilities.CreateContainer;

    private static readonly StorageProviderCapabilities ReadCapabilities = StorageProviderCapabilities.Browse |
        StorageProviderCapabilities.Read | StorageProviderCapabilities.RawStreams | StorageProviderCapabilities.Preview;

    internal Task<StorageLocation> MountAsync(Guid owner, string archivePath, string? password,
        CancellationToken token) => Task.Run(async () =>
    {
        await updateGate.WaitAsync(token);
        try
        {
            ArchiveReadSession session = await service.OpenReadSessionAsync(archivePath, password, token);
            try
            {
                string id = Guid.NewGuid().ToString("N");
                var mount = new Mount(owner, session, BuildNodes(session));
                mounts[id] = mount;
                token.ThrowIfCancellationRequested();
                return Location(id, "");
            }
            catch
            {
                foreach(var pair in mounts.Where(pair => ReferenceEquals(pair.Value.Session, session)))
                    mounts.TryRemove(pair.Key, out _);
                session.Dispose();
                throw;
            }
        }
        finally { updateGate.Release(); }
    }, token);

    internal void CloseOwner(Guid owner)
    {
        List<Exception> failures = [];
        foreach(var pair in mounts.Where(pair => pair.Value.Owner == owner))
            if(mounts.TryRemove(pair.Key, out Mount? mount))
            {
                mount.Session.Dispose();
                if(mount.Workspace is not null)
                {
                    try { DeleteSnapshot(mount.Workspace); }
                    catch(Exception exception) when(exception is IOException or UnauthorizedAccessException)
                    { failures.Add(new IOException(Infrastructure.LocalizationManager.Format("Archive.CleanupFailed", mount.Workspace), exception)); }
                }
            }
        if(failures.Count > 0) throw new AggregateException(failures);
    }

    internal StorageLocation? FindRoot(Guid owner, string archivePath) => mounts
        .Where(pair => pair.Value.Owner == owner && (pair.Value.OriginalLocation ?? pair.Value.Session.ArchivePath).Equals(archivePath,
            StringComparison.OrdinalIgnoreCase))
        .Select(pair => (StorageLocation?)Location(pair.Key, "")).FirstOrDefault();

    internal string SourcePath(StorageLocation location) => Resolve(location).Mount.Session.ArchivePath;
    internal static bool IsArchive(StorageLocation location) => location.ProviderId == ProviderId;
    internal static bool TrySplitDisplayPath(string displayPath, out string archive, out string relative)
    {
        for(int index = displayPath.IndexOf('!'); index >= 0; index = displayPath.IndexOf('!', index + 1))
        {
            if(index + 1 < displayPath.Length && displayPath[index + 1] is '/' or '\\' &&
                ArchivePathPolicy.IsSupportedArchive(displayPath[..index]))
            {
                archive = displayPath[..index];
                relative = displayPath[(index + 2)..];
                return true;
            }
        }
        archive = relative = "";
        return false;
    }

    public string FormatDisplayPath(StorageLocation location)
    {
        var value = Resolve(location);
        return value.Mount.DisplayPath + "!/" + value.Relative.Replace('\\', '/');
    }

    public StorageLocation ResolveDisplayPath(StorageLocation context, string displayPath)
    {
        var value = Resolve(context);
        string prefix = value.Mount.DisplayPath + "!/";
        if(displayPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Location(value.Id, ValidateRelative(displayPath[prefix.Length..]));
        if(displayPath.Contains("://", StringComparison.Ordinal) || Path.IsPathFullyQualified(displayPath))
            return StorageLocation.Parse(displayPath);
        return Location(value.Id, ValidateRelative(displayPath.TrimStart('/', '\\')));
    }

    public Task<IReadOnlyList<StorageItemMetadata>> GetRootsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StorageItemMetadata>>([]);

    public Task<StorageItemMetadata?> GetMetadataAsync(StorageLocation location, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = Resolve(location);
        return Task.FromResult(value.Relative.Length == 0 ? Metadata(value.Id, "", value.Mount, null) :
            value.Mount.Nodes.TryGetValue(value.Relative, out var entry) ? Metadata(value.Id, value.Relative, value.Mount, entry) : null);
    }

    public Task<IReadOnlyList<StorageItemMetadata>> GetChildrenAsync(StorageLocation container,
        bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = Resolve(container);
        if(value.Relative.Length != 0 && (!value.Mount.Nodes.TryGetValue(value.Relative, out var parent) || !parent.IsDirectory))
            throw new DirectoryNotFoundException(FormatDisplayPath(container));
        IReadOnlyList<StorageItemMetadata> children = value.Mount.Nodes
            .Where(pair => string.Equals(Path.GetDirectoryName(pair.Key) ?? "", value.Relative, StringComparison.OrdinalIgnoreCase))
            .Select(pair => Metadata(value.Id, pair.Key, value.Mount, pair.Value))
            .OrderByDescending(item => item.IsContainer).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        return Task.FromResult(children);
    }

    public StorageLocation? GetParent(StorageLocation location)
    {
        var value = Resolve(location);
        return value.Relative.Length == 0 ? value.Mount.Parent ?? new StorageLocation(StorageLocation.LocalProviderId,
            Path.GetDirectoryName(value.Mount.Session.ArchivePath)!) :
            Location(value.Id, Path.GetDirectoryName(value.Relative) ?? "");
    }
    public StorageLocation GetChild(StorageLocation container, string name)
    {
        var value = Resolve(container);
        string child = ArchivePathPolicy.ValidateEntry(name);
        if(child.Contains('\\')) throw new ArgumentException(ArchivePathPolicy.Text("UnsafePath"));
        return Location(value.Id, value.Relative.Length == 0 ? child : value.Relative + "\\" + child);
    }
    public bool AreEquivalent(StorageLocation left, StorageLocation right) =>
        GetComparisonKey(left).Equals(GetComparisonKey(right), StringComparison.Ordinal);
    public string GetComparisonKey(StorageLocation location)
    {
        var value = Resolve(location);
        return value.Id + "/" + value.Relative.ToUpperInvariant();
    }
    public bool IsDescendant(StorageLocation parent, StorageLocation candidate)
    {
        var left = Resolve(parent);
        var right = Resolve(candidate);
        return left.Id == right.Id && !left.Relative.Equals(right.Relative, StringComparison.OrdinalIgnoreCase) &&
            (left.Relative.Length == 0 || right.Relative.StartsWith(left.Relative + "\\", StringComparison.OrdinalIgnoreCase));
    }
    public Task<long> GetTotalSizeAsync(StorageLocation location, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = Resolve(location);
        return Task.FromResult(value.Mount.Nodes.Where(pair => value.Relative.Length == 0 ||
            pair.Key.Equals(value.Relative, StringComparison.OrdinalIgnoreCase) ||
            pair.Key.StartsWith(value.Relative + "\\", StringComparison.OrdinalIgnoreCase)).Sum(pair => pair.Value.Size));
    }
    public Task<Stream> OpenRawReadAsync(StorageLocation location, CancellationToken cancellationToken = default)
    {
        var value = Resolve(location);
        if(!value.Mount.Nodes.TryGetValue(value.Relative, out var entry))
            throw new FileNotFoundException(FormatDisplayPath(location));
        return Task.FromResult(value.Mount.Session.OpenRead(entry, cancellationToken));
    }
    public async Task<FileOperationResult> PullToLocalAsync(StorageLocation source, StorageLocation localDestination,
        IProgressReporter? progress = null, CancellationToken cancellationToken = default)
    {
        if(!localDestination.IsLocal) return ReadOnlyResult();
        var value = Resolve(source);
        try
        {
            await service.CopySelectionAsync(value.Mount.Session, value.Relative, localDestination.OpaqueId, progress, cancellationToken);
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException) { throw; }
        catch(Exception exception) { return FileOperationResult.Fail(exception.Message); }
    }
    public Task<FileOperationResult> PushFromLocalAsync(StorageLocation localSource, StorageLocation destination,
        IProgressReporter? progress = null, CancellationToken cancellationToken = default) =>
        ChangeAsync(destination, relative => new(ArchiveChangeKind.Put, relative, localSource.OpaqueId), progress, cancellationToken);
    public bool IsSameDevice(StorageLocation left, StorageLocation right) => false;

    private StorageItemMetadata Metadata(string id, string relative, Mount mount, ArchiveService.Entry? entry) => new(
        Location(id, relative), entry is null ? Path.GetFileName(mount.Session.ArchivePath) : Path.GetFileName(relative),
        entry is null ? StorageItemKind.Container : entry.IsDirectory ? StorageItemKind.Container : StorageItemKind.File,
        entry?.Size ?? 0, entry?.ModifiedUtc, IsReadOnly: !mount.CanEdit, Capabilities: mount.CanEdit ? Capabilities : ReadCapabilities,
        DisplayPath: mount.DisplayPath + "!/" + relative.Replace('\\', '/'),
        StatusText: ArchivePathPolicy.Text(mount.CanEdit ? "Editable" : "ReadOnly"));

    private (string Id, string Relative, Mount Mount) Resolve(StorageLocation location)
    {
        if(!IsArchive(location)) throw new ArgumentException(nameof(location));
        string[] parts = location.OpaqueId.Split('/', 2);
        if(!mounts.TryGetValue(parts[0], out Mount? mount))
            throw new DirectoryNotFoundException(ArchivePathPolicy.Text("SessionClosed"));
        return (parts[0], parts.Length == 2 ? ValidateRelative(Uri.UnescapeDataString(parts[1])) : "", mount);
    }
    private static string ValidateRelative(string path) => path.Length == 0 ? "" : ArchivePathPolicy.ValidateEntry(path);
    private static StorageLocation Location(string id, string relative) => new(ProviderId, id + "/" + Uri.EscapeDataString(relative));
    private sealed class Mount(Guid owner, ArchiveReadSession session, Dictionary<string, ArchiveService.Entry> nodes)
    {
        internal Guid Owner { get; } = owner;
        internal ArchiveReadSession Session { get; set; } = session;
        internal Dictionary<string, ArchiveService.Entry> Nodes { get; set; } = nodes;
        internal string DisplayPath { get; set; } = session.ArchivePath;
        internal StorageLocation? Parent { get; set; }
        internal string? Workspace { get; set; }
        internal string? OriginalLocation { get; set; }
        internal bool CanEdit => Workspace is null && Session.Type is "zip" or "7z" && !ArchivePathPolicy.IsMultipart(Session.ArchivePath);
    }
    private static FileOperationResult ReadOnlyResult() => FileOperationResult.Fail(ArchivePathPolicy.Text("ReadOnly"));
    private static Exception ReadOnlyError() => new NotSupportedException(ArchivePathPolicy.Text("ReadOnly"));
    public async Task<StorageLocation> CreateUniqueLocationAsync(StorageLocation desiredLocation, bool isContainer, CancellationToken cancellationToken = default)
    {
        var parent = GetParent(desiredLocation) ?? throw ReadOnlyError();
        string name = Path.GetFileName(Resolve(desiredLocation).Relative);
        for(int index = 1; ; index++)
        {
            var candidate = index == 1 ? desiredLocation : GetChild(parent,
                isContainer ? $"{name} ({index})" : $"{Path.GetFileNameWithoutExtension(name)} ({index}){Path.GetExtension(name)}");
            if(await GetMetadataAsync(candidate, cancellationToken) is null) return candidate;
        }
    }
    public Task<IReadOnlyList<StorageDeletionEntry>> BuildDeletionPlanAsync(StorageLocation location, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StorageDeletionEntry>>([new(location, GetTotalSizeAsync(location, cancellationToken).GetAwaiter().GetResult())]);
    public Task<Stream> OpenRawWriteAsync(StorageLocation location, bool overwrite, CancellationToken cancellationToken = default) => throw ReadOnlyError();
    public Task<FileOperationResult> CopyAsync(StorageLocation source, StorageLocation destination, IProgressReporter? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(ReadOnlyResult());
    public Task<FileOperationResult> MoveAsync(StorageLocation source, StorageLocation destination, IProgressReporter? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(ReadOnlyResult());
    public Task<FileOperationResult> DeleteAsync(StorageLocation location, CancellationToken cancellationToken = default) =>
        ChangeAsync(location, relative => new(ArchiveChangeKind.Delete, relative), null, cancellationToken);
    public Task<FileOperationResult> CreateContainerAsync(StorageLocation location, CancellationToken cancellationToken = default) =>
        CreateContainerWithProgressAsync(location, null, cancellationToken);
    internal Task<FileOperationResult> CreateContainerWithProgressAsync(StorageLocation location, IProgressReporter? progress, CancellationToken cancellationToken) =>
        ChangeAsync(location, relative => new(ArchiveChangeKind.CreateDirectory, relative), progress, cancellationToken);
    public Task<FileOperationResult> RenameAsync(StorageLocation location, string newName, CancellationToken cancellationToken = default) =>
        RenameWithProgressAsync(location, newName, null, cancellationToken);
    internal Task<FileOperationResult> RenameWithProgressAsync(StorageLocation location, string newName, IProgressReporter? progress, CancellationToken cancellationToken) =>
        ChangeAsync(location, relative => new(ArchiveChangeKind.Rename, relative,
            Resolve(GetChild(GetParent(location)!.Value, newName)).Relative), progress, cancellationToken);

    public async Task<List<ISystemItem>> GetContainerContentAsync(string path, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default, bool includeHidden = false)
    {
        var location = new StorageLocation(Id, path);
        var metadata = await GetMetadataAsync(location, cancellationToken) ?? throw new DirectoryNotFoundException(path);
        var parent = items.CreateRoot(metadata with { Kind = StorageItemKind.Root });
        return (await GetChildrenAsync(location, includeHidden, cancellationToken)).Select(child => child.IsContainer ?
            (ISystemItem)items.CreateDirectory(child, parent) : items.CreateFile(child, parent)).ToList();
    }
    public Task<Stream> OpenReadAsync(string path, IProgressReporter? progress = null, CancellationToken cancellationToken = default) => OpenRawReadAsync(new(Id, path), cancellationToken);
    public Task<Stream> OpenWriteAsync(string path, bool overwrite, IProgressReporter? progress = null, CancellationToken cancellationToken = default) => throw ReadOnlyError();
    public Task<FileOperationResult> CopyAsync(string sourcePath, string destinationPath, IProgressReporter? progress, CancellationToken cancellationToken = default) => Task.FromResult(ReadOnlyResult());
    public Task<FileOperationResult> MoveAsync(string sourcePath, string destinationPath, IProgressReporter? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(ReadOnlyResult());
    public Task<FileOperationResult> DeleteAsync(string path, CancellationToken cancellationToken = default) => DeleteAsync(new StorageLocation(Id, path), cancellationToken);
    public Task<FileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken = default) => RenameAsync(new StorageLocation(Id, path), newName, cancellationToken);
    public Task<FileOperationResult> CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default) => CreateContainerAsync(new(Id, directoryPath), cancellationToken);
    public async Task<bool> CanReadAsync(string path, CancellationToken cancellationToken = default) => await GetMetadataAsync(new(Id, path), cancellationToken) is not null;
    public Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Resolve(new(Id, path)).Mount.CanEdit);
    public Task<bool> IsReadOnlyAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(!Resolve(new(Id, path)).Mount.CanEdit);
    public Task<bool> IsHiddenAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<FileOperationResult> SetHiddenAsync(string path, bool hidden, CancellationToken cancellationToken = default) => Task.FromResult(ReadOnlyResult());
    public Task<FileOperationResult> SetReadOnlyAsync(string path, bool isReadOnly, CancellationToken cancellationToken = default) => Task.FromResult(ReadOnlyResult());
}

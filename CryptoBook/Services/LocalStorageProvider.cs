using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

/// <summary>
/// Raw local storage provider. It deliberately does not decrypt CryptoBook files:
/// encryption and preview are higher-level, opt-in capabilities.
/// </summary>
public sealed class LocalStorageProvider: IStorageProvider
{
    public string Id => StorageLocation.LocalProviderId;

    public StorageProviderCapabilities Capabilities =>
        StorageProviderCapabilities.Browse |
        StorageProviderCapabilities.Read |
        StorageProviderCapabilities.Write |
        StorageProviderCapabilities.CreateContainer |
        StorageProviderCapabilities.Rename |
        StorageProviderCapabilities.Delete |
        StorageProviderCapabilities.CopyWithinProvider |
        StorageProviderCapabilities.MoveWithinProvider |
        StorageProviderCapabilities.RawStreams |
        StorageProviderCapabilities.Monitor |
        StorageProviderCapabilities.Search |
        StorageProviderCapabilities.Preview |
        StorageProviderCapabilities.OpenExternally |
        StorageProviderCapabilities.Encrypt;

    public string FormatDisplayPath(StorageLocation location) =>
        Normalize(location);

    public StorageLocation ResolveDisplayPath(
        StorageLocation context,
        string displayPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayPath);
        if(!context.IsLocal)
            throw new ArgumentException("Not a local storage location.", nameof(context));
        return new StorageLocation(Id, displayPath);
    }

    public Task<IReadOnlyList<StorageItemMetadata>> GetRootsAsync(
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<StorageItemMetadata>>(
        () => DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => CreateMetadata(drive.RootDirectory, StorageItemKind.Root))
            .ToArray(),
        cancellationToken);

    public Task<StorageItemMetadata?> GetMetadataAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default) => Task.Run(
        () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Normalize(location);
            if(File.Exists(path))
                return CreateMetadata(new FileInfo(path), StorageItemKind.File);
            if(Directory.Exists(path))
            {
                StorageItemKind kind = Path.GetDirectoryName(
                    Path.TrimEndingDirectorySeparator(path)) is null
                        ? StorageItemKind.Root
                        : StorageItemKind.Container;
                return CreateMetadata(new DirectoryInfo(path), kind);
            }
            return null;
        },
        cancellationToken);

    public Task<IReadOnlyList<StorageItemMetadata>> GetChildrenAsync(
        StorageLocation container,
        bool includeHidden = false,
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<StorageItemMetadata>>(
        () =>
        {
            string path = Normalize(container);
            var directory = new DirectoryInfo(path);
            if(!directory.Exists)
                throw new DirectoryNotFoundException(path);

            var result = new List<StorageItemMetadata>();
            foreach(FileSystemInfo entry in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool hidden = (entry.Attributes & FileAttributes.Hidden) != 0;
                if(!includeHidden && hidden)
                    continue;
                result.Add(CreateMetadata(
                    entry,
                    entry is DirectoryInfo
                        ? StorageItemKind.Container
                        : StorageItemKind.File));
            }
            return result;
        },
        cancellationToken);

    public StorageLocation? GetParent(StorageLocation location)
    {
        string path = Normalize(location);
        string? parent = Path.GetDirectoryName(
            Path.TrimEndingDirectorySeparator(path));
        return parent is null ? null : ToLocation(parent);
    }

    public StorageLocation GetChild(StorageLocation container, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ToLocation(Path.Combine(Normalize(container), name));
    }

    public bool AreEquivalent(StorageLocation left, StorageLocation right) =>
        string.Equals(
            NormalizeComparable(left),
            NormalizeComparable(right),
            StringComparison.OrdinalIgnoreCase);

    public bool IsDescendant(StorageLocation parent, StorageLocation candidate)
    {
        string parentPath = NormalizeComparable(parent);
        string candidatePath = NormalizeComparable(candidate);
        return !string.Equals(parentPath, candidatePath, StringComparison.OrdinalIgnoreCase) &&
            candidatePath.StartsWith(
                parentPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task<StorageLocation> CreateUniqueLocationAsync(
        StorageLocation desiredLocation,
        bool isContainer,
        CancellationToken cancellationToken = default)
    {
        string desired = Normalize(desiredLocation);
        string? directory = Path.GetDirectoryName(desired);
        string extension = isContainer ? string.Empty : Path.GetExtension(desired);
        string baseName = isContainer
            ? Path.GetFileName(desired)
            : Path.GetFileNameWithoutExtension(desired);

        for(int index = 1; ; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string suffix = index == 1 ? " - Copy" : $" - Copy ({index})";
            var candidate = ToLocation(Path.Combine(
                directory ?? string.Empty,
                baseName + suffix + extension));
            if(await GetMetadataAsync(candidate, cancellationToken) is null)
                return candidate;
        }
    }

    public Task<long> GetTotalSizeAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default) => Task.Run(
        () => GetSizeCore(Normalize(location), cancellationToken),
        cancellationToken);

    public Task<IReadOnlyList<StorageDeletionEntry>> BuildDeletionPlanAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<StorageDeletionEntry>>(
        () =>
        {
            string path = Normalize(location);
            var result = new List<StorageDeletionEntry>();
            AddDeletionEntries(path, result, cancellationToken);
            return result;
        },
        cancellationToken);

    public Task<Stream> OpenRawReadAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            Normalize(location),
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task<Stream> OpenRawWriteAsync(
        StorageLocation location,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            Normalize(location),
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public async Task<FileOperationResult> CopyAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string sourcePath = Normalize(source);
            string destinationPath = Normalize(destination);
            if(Directory.Exists(sourcePath))
                await CopyDirectoryAsync(sourcePath, destinationPath, progress, cancellationToken);
            else if(File.Exists(sourcePath))
                await CopyFileAsync(sourcePath, destinationPath, progress, cancellationToken);
            else
                return FileOperationResult.Fail("Source not found.");
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return FileOperationResult.Fail(exception.Message);
        }
    }

    public async Task<FileOperationResult> MoveAsync(
        StorageLocation source,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string sourcePath = Normalize(source);
            string destinationPath = Normalize(destination);
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(Directory.Exists(sourcePath))
                    Directory.Move(sourcePath, destinationPath);
                else if(File.Exists(sourcePath))
                    File.Move(sourcePath, destinationPath);
                else
                    throw new FileNotFoundException("Source not found.", sourcePath);
            }, cancellationToken);
            progress?.Report(1, sourcePath);
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return FileOperationResult.Fail(exception.Message);
        }
    }

    public async Task<FileOperationResult> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string path = Normalize(location);
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
                else if(File.Exists(path))
                    File.Delete(path);
                else
                    throw new FileNotFoundException("Path not found.", path);
            }, cancellationToken);
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return FileOperationResult.Fail(exception.Message);
        }
    }

    public async Task<FileOperationResult> CreateContainerAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(
                () => Directory.CreateDirectory(Normalize(location)),
                cancellationToken);
            return FileOperationResult.Ok();
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception exception)
        {
            return FileOperationResult.Fail(exception.Message);
        }
    }

    public async Task<FileOperationResult> RenameAsync(
        StorageLocation location,
        string newName,
        CancellationToken cancellationToken = default)
    {
        StorageLocation? parent = GetParent(location);
        if(parent is null)
            return FileOperationResult.Fail("A storage root cannot be renamed.");
        return await MoveAsync(
            location,
            GetChild(parent.Value, newName),
            cancellationToken: cancellationToken);
    }

    private StorageItemMetadata CreateMetadata(
        FileSystemInfo info,
        StorageItemKind kind)
    {
        FileAttributes attributes = info.Attributes;
        long size = info is FileInfo file ? file.Length : 0;
        return new StorageItemMetadata(
            ToLocation(info.FullName),
            kind == StorageItemKind.Root ? info.FullName : info.Name,
            kind,
            size,
            info.LastWriteTimeUtc,
            (attributes & FileAttributes.Hidden) != 0,
            (attributes & FileAttributes.ReadOnly) != 0,
            Capabilities,
            info.FullName);
    }

    private static StorageLocation ToLocation(string path) =>
        new(StorageLocation.LocalProviderId, Path.GetFullPath(path));

    private string Normalize(StorageLocation location)
    {
        EnsureProvider(location);
        return Path.GetFullPath(location.OpaqueId);
    }

    private string NormalizeComparable(StorageLocation location) =>
        Path.TrimEndingDirectorySeparator(Normalize(location));

    private void EnsureProvider(StorageLocation location)
    {
        if(!location.ProviderId.Equals(Id, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Location belongs to another provider.", nameof(location));
    }

    private static long GetSizeCore(string path, CancellationToken token)
    {
        if(File.Exists(path))
            return new FileInfo(path).Length;
        if(!Directory.Exists(path))
            return 0;

        long total = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach(string file in Directory.EnumerateFiles(path, "*", options))
        {
            token.ThrowIfCancellationRequested();
            total += new FileInfo(file).Length;
        }
        return total;
    }

    private static void AddDeletionEntries(
        string path,
        ICollection<StorageDeletionEntry> result,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(File.Exists(path))
        {
            result.Add(new StorageDeletionEntry(ToLocation(path), new FileInfo(path).Length));
            return;
        }
        if(!Directory.Exists(path))
        {
            result.Add(new StorageDeletionEntry(ToLocation(path), 0));
            return;
        }

        foreach(string child in Directory.EnumerateFileSystemEntries(path))
        {
            token.ThrowIfCancellationRequested();
            FileAttributes attributes = File.GetAttributes(child);
            bool recursiveDirectory =
                (attributes & FileAttributes.Directory) != 0 &&
                (attributes & FileAttributes.ReparsePoint) == 0;
            if(recursiveDirectory)
                AddDeletionEntries(child, result, token);
            else
                result.Add(new StorageDeletionEntry(
                    ToLocation(child),
                    (attributes & FileAttributes.Directory) != 0
                        ? 0
                        : new FileInfo(child).Length));
        }
        result.Add(new StorageDeletionEntry(ToLocation(path), 0));
    }

    private static async Task CopyDirectoryAsync(
        string source,
        string destination,
        IProgressReporter? progress,
        CancellationToken token)
    {
        Directory.CreateDirectory(destination);
        foreach(string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }
        foreach(string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            await CopyFileAsync(file, target, progress, token);
        }
    }

    private static async Task CopyFileAsync(
        string source,
        string destination,
        IProgressReporter? progress,
        CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var output = new FileStream(
            destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        byte[] buffer = new byte[81920];
        long copied = 0;
        int read;
        while((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            copied += read;
            progress?.Report(input.Length == 0 ? 1 : (double)copied / input.Length, source);
        }
    }
}

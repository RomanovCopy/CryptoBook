using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Infrastructure;

using System.IO;

namespace CryptoBook.Services;

public partial class FileSecurityService
{
    private async Task<FileOperationBatchResult> CreateDirectoryProtectedCopiesAsync(
        IReadOnlyList<ISystemItem> sources,
        string destinationDirectory,
        IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        using IDisposable? timerPause = keyResetService?.Pause();
        progress?.Report(null, sources[0].FullPath);

        // Снимок всего выбора строится до первой записи: назначение может
        // находиться внутри одного из выбранных деревьев.
        List<ProtectedCopyGroup> work = await Task.Run(async () =>
        {
            var groups = new List<ProtectedCopyGroup>();
            StorageLocation destination = storage.Resolve(destinationDirectory);
            var reservedRoots = new HashSet<string>(StringComparer.Ordinal);
            foreach(ISystemItem source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if(source is IFileItem)
                    {
                        groups.Add(new(source.FullPath, destination, [source]));
                        continue;
                    }
                    if(source is not IDirectoryItem || !source.Location.IsLocal)
                        throw new NotSupportedException(LocalizationManager.Format(
                            "Security.UnsupportedItemType", source.GetType().Name));

                    var root = new DirectoryInfo(source.Location.OpaqueId);
                    if(!root.Exists)
                        throw new DirectoryNotFoundException(LocalizationManager.Format(
                            "Security.SourceDirectoryNotFound", source.FullPath));
                    // Не выходим из выбранного дерева по ссылкам/junction.
                    if(root.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        throw new IOException(LocalizationManager.GetString("Security.LinkedDirectoryNotSupported"));

                    StorageLocation targetRoot = storage.GetChild(destination, root.Name);
                    IStorageProvider provider = storage.GetProvider(targetRoot);
                    for(int index = 2; ; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string key = provider.GetComparisonKey(targetRoot);
                        if(!reservedRoots.Contains(key) &&
                           await storage.GetMetadataAsync(targetRoot, cancellationToken) is null)
                        {
                            reservedRoots.Add(key);
                            break;
                        }
                        targetRoot = storage.GetChild(destination, $"{root.Name} ({index})");
                    }

                    var pending = new Stack<(DirectoryInfo Source, StorageLocation Target)>();
                    pending.Push((root, targetRoot));
                    var options = new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.ReparsePoint,
                        IgnoreInaccessible = false,
                        ReturnSpecialDirectories = false
                    };
                    while(pending.TryPop(out var directory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(null, directory.Source.FullName);
                        try
                        {
                            var files = new List<ISystemItem>();
                            foreach(FileInfo file in directory.Source.EnumerateFiles("*", options))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                files.Add(new FileItem
                                {
                                    FullPath = file.FullName,
                                    Name = Path.GetFileNameWithoutExtension(file.Name),
                                    Extension = file.Extension,
                                    Size = file.Length
                                });
                            }
                            groups.Add(new(directory.Source.FullName, directory.Target, files));
                            foreach(DirectoryInfo child in directory.Source.EnumerateDirectories("*", options))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if(destination.IsLocal &&
                                   !PathsEqual(destination.OpaqueId, root.FullName) &&
                                   IsSameOrDescendantPath(destination.OpaqueId, root.FullName) &&
                                   IsSameOrDescendantPath(child.FullName, destination.OpaqueId))
                                    continue;
                                pending.Push((child, storage.GetChild(directory.Target, child.Name)));
                            }
                        }
                        catch(OperationCanceledException) { throw; }
                        catch(Exception ex)
                        {
                            groups.Add(new(directory.Source.FullName, directory.Target, [],
                                ex.Message));
                        }
                    }
                }
                catch(OperationCanceledException) { throw; }
                catch(Exception ex)
                {
                    groups.Add(new(source.FullPath, destination, [],
                        ex.Message));
                }
            }
            return groups;
        }, cancellationToken);

        var results = new List<FileOperationResult>();
        int completed = 0;
        int skipped = 0;
        long total = work.Aggregate(0L, (sum, group) => SaturatingAdd(sum, group.WorkUnits));
        long done = 0;
        foreach(ProtectedCopyGroup group in work)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(group.Error is not null)
                    throw new IOException(group.Error);

                StorageItemMetadata? existing = await storage.GetMetadataAsync(group.Destination, cancellationToken);
                if(existing is null)
                {
                    FileOperationResult created = await storage.GetProvider(group.Destination)
                        .CreateContainerAsync(group.Destination, cancellationToken);
                    if(!created.Success)
                        throw new IOException(created.ErrorMessage);
                }
                else if(!existing.IsContainer)
                    throw new IOException(LocalizationManager.GetString("Security.DestinationMustBeDirectory"));

                if(group.Files.Count > 0)
                {
                    FileOperationBatchResult result = await CreateProtectedCopiesAsync(
                        group.Files, storage.Format(group.Destination),
                        progress is null ? null : new BatchSourceProgressReporter(
                            progress, done, group.WorkUnits, total, group.SourcePath),
                        cancellationToken);
                    results.AddRange(result.Results);
                    completed += result.CompletedCount;
                    skipped += result.SkippedCount;
                    if(result.Canceled)
                        return new(results, completed, skipped, true, completed > 0);
                }
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                return new(results, completed, skipped, true, completed > 0);
            }
            catch(Exception ex)
            {
                results.Add(FileOperationResult.Fail(FormatPathError(group.SourcePath, ex.Message)));
            }
            done = SaturatingAdd(done, group.WorkUnits);
            progress?.Report(CalculateBatchProgress(done, total), group.SourcePath);
        }
        return new(results, completed, skipped, false,
            completed > 0 && results.Any(result => !result.Success));
    }

    private sealed record ProtectedCopyGroup(
        string SourcePath,
        StorageLocation Destination,
        IReadOnlyList<ISystemItem> Files,
        string? Error = null)
    {
        public long WorkUnits { get; } = Math.Max(1L,
            Files.Aggregate(0L, (sum, file) => SaturatingAdd(sum, Math.Max(1L, file.Size))));
    }
}

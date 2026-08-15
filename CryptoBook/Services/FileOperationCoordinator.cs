using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class FileOperationCoordinator: IFileOperationCoordinator
    {
        private readonly IFileManagerService _fileManager;
        private readonly IProgressDialogService _progressDialog;
        private readonly IFileConflictResolver _conflictResolver;

        public FileOperationCoordinator(
            IFileManagerService fileManager,
            IProgressDialogService progressDialog,
            IFileConflictResolver conflictResolver)
        {
            _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
            _progressDialog = progressDialog ?? throw new ArgumentNullException(nameof(progressDialog));
            _conflictResolver = conflictResolver ?? throw new ArgumentNullException(nameof(conflictResolver));
        }

        public async Task<FileOperationBatchResult> TransferAsync(
            IEnumerable<string> sourcePaths,
            string destinationDirectory,
            FileTransferKind operation,
            CancellationToken cancellationToken = default,
            Func<Task>? synchronizeViewAsync = null)
        {
            ArgumentNullException.ThrowIfNull(sourcePaths);
            if(string.IsNullOrWhiteSpace(destinationDirectory))
                throw new ArgumentException("Destination directory is empty.", nameof(destinationDirectory));

            string[] sources = NormalizeTopLevelSources(sourcePaths);
            if(sources.Length == 0)
                return EmptyResult();

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var plans = await CreateTransferPlansAsync(
                sources,
                destinationDirectory,
                operation,
                linkedCancellation.Token);
            if(plans.Canceled)
                return new FileOperationBatchResult([], 0, plans.SkippedCount, true, false);
            if(plans.Items.Count == 0)
                return new FileOperationBatchResult([], 0, plans.SkippedCount, false, false);

            string operationName = LocalizationManager.GetString(
                operation == FileTransferKind.Copy
                    ? "Explorer.Copying"
                    : "Explorer.Moving");
            var results = new List<FileOperationResult>(plans.Items.Count);
            int completed = 0;

            try
            {
                return await _progressDialog.RunAsync(
                    operationName,
                    async (progress, dialogToken) =>
                    {
                        try
                        {
                            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                                linkedCancellation.Token,
                                dialogToken);
                            CancellationToken token = operationCancellation.Token;
                            progress.Report(
                                null,
                                LocalizationManager.GetString("Explorer.CalculatingSize"));
                            long[] itemSizes = await Task.Run(
                                () => plans.Items
                                    .Select(item => GetItemSize(item.SourcePath, token))
                                    .ToArray(),
                                token);
                            long totalBytes = itemSizes.Sum();
                            long completedBytes = 0;

                            for(int index = 0; index < plans.Items.Count; index++)
                            {
                                TransferPlan plan = plans.Items[index];
                                long itemSize = itemSizes[index];
                                token.ThrowIfCancellationRequested();
                                if(plan.ReplaceExisting)
                                {
                                    FileOperationResult deleteResult = await _fileManager.DeleteAsync(
                                        plan.DestinationPath,
                                        token);
                                    if(!deleteResult.Success)
                                    {
                                        results.Add(deleteResult);
                                        break;
                                    }
                                }

                                var itemProgress = new AggregateProgressReporter(
                                    progress,
                                    completedBytes,
                                    itemSize,
                                    totalBytes,
                                    plan.SourcePath);
                                FileOperationResult result;
                                if(operation == FileTransferKind.Copy)
                                {
                                    result = await _fileManager.CopyAsync(
                                        plan.SourcePath,
                                        plan.DestinationPath,
                                        itemProgress,
                                        token);
                                }
                                else if(IsCrossVolumeLocalMove(
                                    plan.SourcePath,
                                    plan.DestinationPath))
                                {
                                    result = await _fileManager.CopyAsync(
                                        plan.SourcePath,
                                        plan.DestinationPath,
                                        itemProgress,
                                        token);
                                    if(result.Success)
                                    {
                                        token.ThrowIfCancellationRequested();
                                        result = await _fileManager.DeleteAsync(
                                            plan.SourcePath,
                                            token);
                                    }
                                }
                                else
                                {
                                    result = await _fileManager.MoveAsync(
                                        plan.SourcePath,
                                        plan.DestinationPath,
                                        itemProgress,
                                        token);
                                }
                                results.Add(result);
                                if(!result.Success)
                                    break;

                                completed++;
                                completedBytes += itemSize;
                                progress.Report(
                                    CalculateOverall(completedBytes, totalBytes, completed, plans.Items.Count),
                                    plan.SourcePath);
                            }

                            return new FileOperationBatchResult(
                                results,
                                completed,
                                plans.SkippedCount,
                                false,
                                completed > 0);
                        }
                        finally
                        {
                            await SynchronizeViewAsync(progress, synchronizeViewAsync);
                        }
                    });
            }
            catch(OperationCanceledException)
            {
                return new FileOperationBatchResult(
                    results,
                    completed,
                    plans.SkippedCount,
                    true,
                    completed > 0);
            }
        }

        public async Task<FileOperationBatchResult> DeleteAsync(
            IEnumerable<string> sourcePaths,
            CancellationToken cancellationToken = default,
            Func<Task>? synchronizeViewAsync = null)
        {
            ArgumentNullException.ThrowIfNull(sourcePaths);
            string[] sources = NormalizeTopLevelSources(sourcePaths);
            if(sources.Length == 0)
                return EmptyResult();

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var results = new List<FileOperationResult>();
            int completed = 0;

            try
            {
                return await _progressDialog.RunAsync(
                    LocalizationManager.GetString("Explorer.Deleting"),
                    async (progress, dialogToken) =>
                    {
                        try
                        {
                            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                                linkedCancellation.Token,
                                dialogToken);
                            CancellationToken token = operationCancellation.Token;
                            progress.Report(
                                null,
                                LocalizationManager.GetString("Explorer.CalculatingSize"));
                            IReadOnlyList<DeletionEntry> entries = await Task.Run(
                                () => BuildDeletionPlan(sources, token),
                                token);
                            long totalBytes = entries.Sum(entry => entry.Size);
                            long completedBytes = 0;

                            // Удаление намеренно не транзакционно: отмена сохраняет уже удалённые элементы.
                            foreach(DeletionEntry entry in entries)
                            {
                                token.ThrowIfCancellationRequested();
                                FileOperationResult result = await _fileManager.DeleteAsync(entry.Path, token);
                                results.Add(result);
                                if(!result.Success)
                                    break;

                                completed++;
                                completedBytes += entry.Size;
                                progress.Report(
                                    CalculateOverall(completedBytes, totalBytes, completed, entries.Count),
                                    entry.Path);
                            }

                            return new FileOperationBatchResult(
                                results,
                                completed,
                                0,
                                false,
                                completed > 0);
                        }
                        finally
                        {
                            await SynchronizeViewAsync(progress, synchronizeViewAsync);
                        }
                    });
            }
            catch(OperationCanceledException)
            {
                return new FileOperationBatchResult(results, completed, 0, true, completed > 0);
            }
            catch(Exception exception)
            {
                results.Add(FileOperationResult.Fail(exception.Message));
                return new FileOperationBatchResult(results, completed, 0, false, completed > 0);
            }
        }

        private static async Task SynchronizeViewAsync(
            IProgressReporter progress,
            Func<Task>? synchronizeViewAsync)
        {
            if(synchronizeViewAsync is null)
                return;

            progress.Report(
                null,
                LocalizationManager.GetString("Explorer.RefreshingAfterOperation"));
            await synchronizeViewAsync();
        }

        private async Task<TransferPlanSet> CreateTransferPlansAsync(
            IReadOnlyList<string> sources,
            string destinationDirectory,
            FileTransferKind operation,
            CancellationToken token)
        {
            var plans = new List<TransferPlan>(sources.Count);
            FileConflictDecision? applyToAll = null;
            int skipped = 0;

            foreach(string source in sources)
            {
                token.ThrowIfCancellationRequested();
                bool isDirectory = Directory.Exists(ToNativePath(source));
                ValidateDestination(source, destinationDirectory, isDirectory);

                string destination = CombinePath(destinationDirectory, GetItemName(source));
                if(PathsEqual(source, destination))
                {
                    if(operation == FileTransferKind.Move)
                    {
                        skipped++;
                        continue;
                    }

                    destination = CreateUniquePath(destination, isDirectory);
                }

                bool replace = false;
                if(PathExists(destination) && !PathsEqual(source, destination))
                {
                    FileConflictDecision decision = applyToAll ?? await _conflictResolver.ResolveAsync(
                        source,
                        destination,
                        isDirectory,
                        token);
                    if(decision.ApplyToAll)
                        applyToAll = decision;

                    switch(decision.Action)
                    {
                        case FileConflictAction.Cancel:
                            return new TransferPlanSet(plans, skipped, true);
                        case FileConflictAction.Skip:
                            skipped++;
                            continue;
                        case FileConflictAction.KeepBoth:
                            destination = CreateUniquePath(destination, isDirectory);
                            break;
                        case FileConflictAction.Replace:
                            replace = true;
                            break;
                    }
                }

                plans.Add(new TransferPlan(source, destination, replace));
            }

            return new TransferPlanSet(plans, skipped, false);
        }

        internal static void ValidateDestination(
            string sourcePath,
            string destinationDirectory,
            bool sourceIsDirectory)
        {
            if(!sourceIsDirectory)
                return;

            string source = NormalizeLocalPath(sourcePath);
            string destination = NormalizeLocalPath(destinationDirectory);
            if(PathsEqual(source, destination) ||
               destination.StartsWith(
                   source + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    LocalizationManager.GetString("Explorer.DropIntoSelfError"));
            }
        }

        private static IReadOnlyList<DeletionEntry> BuildDeletionPlan(
            IEnumerable<string> sourcePaths,
            CancellationToken token)
        {
            var entries = new List<DeletionEntry>();
            foreach(string sourcePath in sourcePaths)
            {
                token.ThrowIfCancellationRequested();
                string nativePath = ToNativePath(sourcePath);
                if(File.Exists(nativePath))
                {
                    entries.Add(new DeletionEntry(sourcePath, new FileInfo(nativePath).Length));
                }
                else if(Directory.Exists(nativePath))
                {
                    AddDirectoryDeletionEntries(nativePath, entries, token);
                }
                else
                {
                    entries.Add(new DeletionEntry(sourcePath, 0));
                }
            }

            return entries;
        }

        private static void AddDirectoryDeletionEntries(
            string directoryPath,
            ICollection<DeletionEntry> entries,
            CancellationToken token)
        {
            foreach(string entryPath in Directory.EnumerateFileSystemEntries(directoryPath))
            {
                token.ThrowIfCancellationRequested();
                FileAttributes attributes = File.GetAttributes(entryPath);
                bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                bool isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
                if(isDirectory && !isReparsePoint)
                {
                    AddDirectoryDeletionEntries(entryPath, entries, token);
                }
                else
                {
                    long size = isDirectory ? 0 : new FileInfo(entryPath).Length;
                    entries.Add(new DeletionEntry(entryPath, size));
                }
            }

            entries.Add(new DeletionEntry(directoryPath, 0));
        }

        private static string[] NormalizeTopLevelSources(IEnumerable<string> sourcePaths)
        {
            string[] paths = sourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return paths
                .Where(candidate => !paths.Any(parent =>
                    !PathsEqual(parent, candidate) &&
                    IsChildPath(parent, candidate)))
                .ToArray();
        }

        private static bool IsChildPath(string parentPath, string candidatePath)
        {
            try
            {
                string parent = NormalizeLocalPath(parentPath);
                string candidate = NormalizeLocalPath(candidatePath);
                return candidate.StartsWith(
                    parent + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static long GetItemSize(string path, CancellationToken token)
        {
            string nativePath = ToNativePath(path);
            if(File.Exists(nativePath))
                return new FileInfo(nativePath).Length;
            if(!Directory.Exists(nativePath))
                return 0;

            long total = 0;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach(string file in Directory.EnumerateFiles(nativePath, "*", options))
            {
                token.ThrowIfCancellationRequested();
                total += new FileInfo(file).Length;
            }

            return total;
        }

        private static string CreateUniquePath(string destinationPath, bool isDirectory)
        {
            string native = ToNativePath(destinationPath);
            string? directory = Path.GetDirectoryName(native);
            string extension = isDirectory ? string.Empty : Path.GetExtension(native);
            string baseName = isDirectory
                ? Path.GetFileName(native)
                : Path.GetFileNameWithoutExtension(native);
            for(int index = 2; ; index++)
            {
                string suffix = index == 2 ? " - Copy" : $" - Copy ({index})";
                string candidate = Path.Combine(directory ?? string.Empty, baseName + suffix + extension);
                if(!PathExists(candidate))
                    return RestoreScheme(destinationPath, candidate);
            }
        }

        private static string CombinePath(string directory, string name)
        {
            string native = Path.Combine(ToNativePath(directory), name);
            return RestoreScheme(directory, native);
        }

        private static string RestoreScheme(string original, string nativePath) =>
            original.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                ? $"local://{nativePath}"
                : nativePath;

        private static string GetItemName(string path) =>
            Path.GetFileName(ToNativePath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        private static bool PathExists(string path)
        {
            string native = ToNativePath(path);
            return File.Exists(native) || Directory.Exists(native);
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(
                    NormalizeLocalPath(left),
                    NormalizeLocalPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string NormalizeLocalPath(string path) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(ToNativePath(path)));

        private static string ToNativePath(string path) =>
            path.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                ? path[8..]
                : path;

        private static bool IsCrossVolumeLocalMove(string sourcePath, string destinationPath)
        {
            string? sourceRoot = Path.GetPathRoot(NormalizeLocalPath(sourcePath));
            string? destinationRoot = Path.GetPathRoot(NormalizeLocalPath(destinationPath));
            return !string.IsNullOrWhiteSpace(sourceRoot) &&
                   !string.IsNullOrWhiteSpace(destinationRoot) &&
                   !string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static double CalculateOverall(
            long completedBytes,
            long totalBytes,
            int completedItems,
            int totalItems) =>
            totalBytes > 0
                ? Math.Clamp((double)completedBytes / totalBytes, 0, 1)
                : Math.Clamp((double)completedItems / Math.Max(1, totalItems), 0, 1);

        private static FileOperationBatchResult EmptyResult() => new([], 0, 0, false, false);

        private sealed record TransferPlan(
            string SourcePath,
            string DestinationPath,
            bool ReplaceExisting);

        private sealed record TransferPlanSet(
            IReadOnlyList<TransferPlan> Items,
            int SkippedCount,
            bool Canceled);

        private sealed record DeletionEntry(string Path, long Size);

        private sealed class AggregateProgressReporter: IProgressReporter
        {
            private readonly IProgressReporter _outer;
            private readonly long _completedBytes;
            private readonly long _itemSize;
            private readonly long _totalBytes;
            private readonly string _itemName;

            public AggregateProgressReporter(
                IProgressReporter outer,
                long completedBytes,
                long itemSize,
                long totalBytes,
                string itemName)
            {
                _outer = outer;
                _completedBytes = completedBytes;
                _itemSize = itemSize;
                _totalBytes = totalBytes;
                _itemName = itemName;
            }

            public void Report(double? value, string? currentInfo = null)
            {
                if(value is null || _totalBytes == 0)
                {
                    _outer.Report(value, currentInfo ?? _itemName);
                    return;
                }

                double overall = (_completedBytes + _itemSize * value.Value) / _totalBytes;
                _outer.Report(Math.Clamp(overall, 0, 1), currentInfo ?? _itemName);
            }
        }
    }
}

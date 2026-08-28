using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class FileOperationCoordinator: IFileOperationCoordinator
    {
        private readonly IFileManagerService _fileManager;
        private readonly IProgressDialogService _progressDialog;
        private readonly IFileConflictResolver _conflictResolver;
        private readonly IStorageFacade _storage;

        public FileOperationCoordinator(
            IFileManagerService fileManager,
            IProgressDialogService progressDialog,
            IFileConflictResolver conflictResolver,
            IStorageFacade? storageFacade = null)
        {
            _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
            _progressDialog = progressDialog ?? throw new ArgumentNullException(nameof(progressDialog));
            _conflictResolver = conflictResolver ?? throw new ArgumentNullException(nameof(conflictResolver));
            _storage = storageFacade ?? new StorageFacade([new LocalStorageProvider()]);
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
                            long[] itemSizes = await Task.WhenAll(plans.Items
                                .Select(item => _storage.GetTotalSizeAsync(
                                    _storage.Resolve(item.SourcePath),
                                    token)));
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
                            IReadOnlyList<StorageDeletionEntry> entries =
                                await BuildDeletionPlanAsync(sources, token);
                            long totalBytes = entries.Sum(entry => entry.Size);
                            long completedBytes = 0;

                            // Удаление намеренно не транзакционно: отмена сохраняет уже удалённые элементы.
                            foreach(StorageDeletionEntry entry in entries)
                            {
                                token.ThrowIfCancellationRequested();
                                string path = _storage.Format(entry.Location);
                                FileOperationResult result = await _fileManager.DeleteAsync(path, token);
                                results.Add(result);
                                if(!result.Success)
                                    break;

                                completed++;
                                completedBytes += entry.Size;
                                progress.Report(
                                    CalculateOverall(completedBytes, totalBytes, completed, entries.Count),
                                    path);
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
                StorageLocation sourceLocation = _storage.Resolve(source);
                StorageLocation destinationContainer = _storage.Resolve(destinationDirectory);
                StorageItemMetadata? metadata = await _storage.GetMetadataAsync(
                    sourceLocation,
                    token);
                bool isDirectory = metadata?.IsContainer == true;
                ValidateDestination(sourceLocation, destinationContainer, isDirectory);

                string itemName = metadata?.Name ?? source;
                StorageLocation destinationLocation = _storage.GetChild(
                    destinationContainer,
                    itemName);
                if(_storage.AreEquivalent(sourceLocation, destinationLocation))
                {
                    if(operation == FileTransferKind.Move)
                    {
                        skipped++;
                        continue;
                    }

                    destinationLocation = await _storage.CreateUniqueLocationAsync(
                        destinationLocation,
                        isDirectory,
                        token);
                }

                bool replace = false;
                if(await _storage.GetMetadataAsync(destinationLocation, token) is not null &&
                   !_storage.AreEquivalent(sourceLocation, destinationLocation))
                {
                    string destination = _storage.Format(destinationLocation);
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
                            destinationLocation = await _storage.CreateUniqueLocationAsync(
                                destinationLocation,
                                isDirectory,
                                token);
                            break;
                        case FileConflictAction.Replace:
                            replace = true;
                            break;
                    }
                }

                plans.Add(new TransferPlan(
                    source,
                    _storage.Format(destinationLocation),
                    replace));
            }

            return new TransferPlanSet(plans, skipped, false);
        }

        internal static void ValidateDestination(
            string sourcePath,
            string destinationDirectory,
            bool sourceIsDirectory)
        {
            var storage = new StorageFacade([new LocalStorageProvider()]);
            StorageLocation source = storage.Resolve(sourcePath);
            StorageLocation destination = storage.Resolve(destinationDirectory);
            ValidateDestination(storage, source, destination, sourceIsDirectory);
        }

        private void ValidateDestination(
            StorageLocation source,
            StorageLocation destination,
            bool sourceIsDirectory) =>
            ValidateDestination(_storage, source, destination, sourceIsDirectory);

        private static void ValidateDestination(
            IStorageFacade storage,
            StorageLocation source,
            StorageLocation destination,
            bool sourceIsDirectory)
        {
            if(sourceIsDirectory &&
               (storage.AreEquivalent(source, destination) ||
                storage.IsDescendant(source, destination)))
            {
                throw new InvalidOperationException(
                    LocalizationManager.GetString("Explorer.DropIntoSelfError"));
            }
        }

        private async Task<IReadOnlyList<StorageDeletionEntry>> BuildDeletionPlanAsync(
            IEnumerable<string> sourcePaths,
            CancellationToken token)
        {
            var entries = new List<StorageDeletionEntry>();
            foreach(string sourcePath in sourcePaths)
            {
                token.ThrowIfCancellationRequested();
                entries.AddRange(await _storage.BuildDeletionPlanAsync(
                    _storage.Resolve(sourcePath),
                    token));
            }
            return entries;
        }

        private string[] NormalizeTopLevelSources(IEnumerable<string> sourcePaths)
        {
            string[] paths = sourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return paths
                .Where(candidate => !paths.Any(parent =>
                    !_storage.AreEquivalent(
                        _storage.Resolve(parent),
                        _storage.Resolve(candidate)) &&
                    IsChildPath(parent, candidate)))
                .ToArray();
        }

        private bool IsChildPath(string parentPath, string candidatePath)
        {
            try
            {
                return _storage.IsDescendant(
                    _storage.Resolve(parentPath),
                    _storage.Resolve(candidatePath));
            }
            catch
            {
                return false;
            }
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

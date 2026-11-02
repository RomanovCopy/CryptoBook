using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;
using System.Security.Cryptography;

using TransferPlanSet = CryptoBook.Services.FileOperationCoordinator.TransferPlanSet;

namespace CryptoBook.Services;

/// <summary>Owns native local paths; the coordinator only routes provider locations.</summary>
internal sealed class LocalFileOperationService(IFileManagerService fileManager, IStorageFacade storage,
    IRecycleBinService recycleBin)
{
    private readonly IFileManagerService _fileManager = fileManager;
    private readonly IStorageFacade _storage = storage;
    internal Task<bool> HasRecoveryAsync(string directory, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        StorageLocation location = _storage.Resolve(directory);
        return location.IsLocal && LocalOperationJournal.HasRecovery(location.OpaqueId);
    }, token);
    public Task<FileOperationBatchResult> RestoreAsync(string directory,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        StorageLocation location = _storage.Resolve(directory);
        if(!location.IsLocal) throw new NotSupportedException();
        var restored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> errors = LocalOperationJournal.Recover(location.OpaqueId, restored);
        return new FileOperationBatchResult(errors.Select(e => FileOperationResult.Fail(e)).ToArray(),
            0, 0, false, errors.Count > 0) { RestoredPaths = restored };
    }, cancellationToken);

    internal async Task<FileOperationBatchResult> TransferAsync(TransferPlanSet plans,
        FileTransferKind operation, long[] sizes, long totalBytes, IProgressReporter progress,
        CancellationToken token)
    {
        string firstTarget = _storage.Resolve(plans.Items[0].DestinationPath).OpaqueId;
        using var journal = LocalOperationJournal.Create(Path.GetDirectoryName(firstTarget)!);
        var results = new List<FileOperationResult>();
        long completedBytes = 0;
        try
        {
            for(int index = 0; index < plans.Items.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var plan = plans.Items[index];
                string source = _storage.Resolve(plan.SourcePath).OpaqueId;
                string destination = _storage.Resolve(plan.DestinationPath).OpaqueId;
                string stage = journal.Allocate(destination);
                string readSource = source;
                if(operation == FileTransferKind.Move)
                {
                    readSource = journal.Allocate(source);
                    journal.Move(source, readSource);
                }
                if(operation == FileTransferKind.Move && LocalOperationJournal.Identity(readSource)[..8] ==
                    LocalOperationJournal.Identity(Path.GetDirectoryName(stage)!)[..8])
                    journal.Move(readSource, stage);
                else
                {
                    var itemProgress = new FileOperationCoordinator.AggregateProgressReporter(progress, completedBytes,
                        sizes[index], totalBytes, plan.SourcePath);
                    FileOperationResult copied = await _fileManager.CopyAsync(readSource, stage, itemProgress, token);
                    if(!copied.Success) throw new IOException(copied.ErrorMessage);
                    if(operation == FileTransferKind.Move)
                        await VerifyCopyAsync(readSource, stage, token);
                }
                token.ThrowIfCancellationRequested();
                if(plan.ReplaceExisting && Path.Exists(destination))
                    journal.Move(destination, journal.Allocate(destination), plan.DestinationIdentity);
                journal.Move(stage, destination); // No overwrite even if a new target appeared.
                results.Add(FileOperationResult.Ok());
                completedBytes += sizes[index];
                progress.Report(FileOperationCoordinator.CalculateOverall(completedBytes, totalBytes, index + 1, plans.Items.Count),
                    plan.SourcePath);
            }
            token.ThrowIfCancellationRequested();
            journal.Complete();
            return new(results, results.Count, plans.SkippedCount, false, results.Count > 0);
        }
        catch(Exception e)
        {
            string? recoveryError = journal.IsCommitted ? e.Message : journal.Rollback();
            var failures = new List<FileOperationResult>();
            if(e is not OperationCanceledException || recoveryError is not null)
                failures.Add(FileOperationResult.Fail(string.Join(Environment.NewLine,
                    new[] { e is OperationCanceledException ? null : e.Message, recoveryError }
                    .Where(s => !string.IsNullOrEmpty(s)))));
            return new(failures, recoveryError is null ? 0 : results.Count, plans.SkippedCount,
                e is OperationCanceledException, recoveryError is not null);
        }
    }

    internal async Task<FileOperationBatchResult> DeleteAsync(string[] sources, IProgressReporter progress,
        CancellationToken token)
    {
        int count = 0;
        var results = new List<FileOperationResult>();
        try
        {
            foreach(string path in sources)
            {
                token.ThrowIfCancellationRequested();
                string source = _storage.Resolve(path).OpaqueId;
                FileOperationResult result = await recycleBin.RecycleAsync(source, token);
                results.Add(result);
                if(!result.Success) return new(results, count, 0, false, count > 0);
                progress.Report((double)++count / sources.Length, path);
            }
            return new(results, count, 0, token.IsCancellationRequested, count > 0);
        }
        catch(OperationCanceledException) { return new(results, count, 0, true, count > 0); }
        catch(Exception e)
        {
            results.Add(FileOperationResult.Fail(e.Message));
            return new(results, count, 0, false, count > 0);
        }
    }

    internal static async Task VerifyCopyAsync(string source, string destination, CancellationToken token)
    {
        if((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException(LocalizationManager.GetString("Explorer.TransferVerificationFailed"));
        if(Directory.Exists(source))
        {
            string[] children = Directory.GetFileSystemEntries(source);
            if(children.Length != Directory.GetFileSystemEntries(destination).Length)
                throw new IOException(LocalizationManager.GetString("Explorer.TransferVerificationFailed"));
            foreach(string child in children)
            {
                token.ThrowIfCancellationRequested();
                await VerifyCopyAsync(child, Path.Combine(destination, Path.GetFileName(child)), token);
            }
            return;
        }
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] sourceHash = await SHA256.HashDataAsync(input, token);
        byte[] targetHash = await SHA256.HashDataAsync(output, token);
        if(input.Length != output.Length || !CryptographicOperations.FixedTimeEquals(sourceHash, targetHash))
            throw new IOException(LocalizationManager.GetString("Explorer.TransferVerificationFailed"));
    }

}

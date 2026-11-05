using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IRecycleBinService : IService
{
    Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default);

    Task<RecycleBinDeletionPlan> PlanDeletionAsync(IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default) => Task.FromResult(new RecycleBinDeletionPlan([]));

    async Task<FileOperationBatchResult> RecycleBatchAsync(IReadOnlyList<string> paths,
        IProgressReporter? progress = null, CancellationToken cancellationToken = default)
    {
        var results = new List<FileOperationResult>();
        int completed = 0;
        try
        {
            foreach(string path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileOperationResult result = await RecycleAsync(path, cancellationToken);
                results.Add(result);
                if(!result.Success) break;
                completed++;
                progress?.Report((double)completed / paths.Count, path);
            }
            return new(results, completed, 0, cancellationToken.IsCancellationRequested, completed > 0);
        }
        catch(OperationCanceledException)
        {
            return new(results, completed, 0, true, completed > 0);
        }
        catch(Exception exception)
        {
            results.Add(FileOperationResult.Fail(exception.Message));
            return new(results, completed, 0, false, completed > 0);
        }
    }

    Task<FileOperationBatchResult> DeletePermanentlyAsync(IReadOnlyList<string> paths,
        IProgressReporter? progress = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

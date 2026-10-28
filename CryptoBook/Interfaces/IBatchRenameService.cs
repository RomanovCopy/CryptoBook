using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IBatchRenameService: IService
{
    Task<IReadOnlyList<BatchRenameEntry>> PreviewAsync(
        IReadOnlyList<string> paths, BatchRenameOptions options,
        CancellationToken cancellationToken = default);
    Task<BatchRenameResult> RenameAsync(
        IReadOnlyList<BatchRenameEntry> entries, IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}

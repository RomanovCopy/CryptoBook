using CryptoBook.DTO;
using CryptoBook.Services;

namespace CryptoBook.Interfaces;

public interface IWorkspaceSearchIndex: IService
{
    WorkspaceContentSearchOptions Options { get; }

    IDocumentTextExtractor? FindExtractor(string extension);

    Task<WorkspaceSearchIndexUpdateOutcome> UpdateAsync(
        string workspaceRoot,
        IProgress<WorkspaceContentSearchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceIndexedDocument>> SearchAsync(
        string workspaceRoot,
        string query,
        CancellationToken cancellationToken = default);
}

using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IArchiveService: IService
{
    Task<ArchiveOperationResult> CreateAsync(IReadOnlyList<string> sources, string outputPath,
        ArchiveFormat format, string? password = null, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
    Task<ArchiveOperationResult> ExtractAsync(string archivePath, string outputDirectory,
        string? password = null, ArchiveExtractionLimits? limits = null,
        IProgressReporter? progress = null, CancellationToken cancellationToken = default);
    Task<ArchiveOperationResult> TestAsync(string archivePath, string? password = null,
        IProgressReporter? progress = null, CancellationToken cancellationToken = default);
}

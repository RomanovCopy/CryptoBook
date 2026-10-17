using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IFileDetailsService: IService
{
    Task<IReadOnlyList<FilePropertyEntry>> ReadAsync(
        FileDetailsTarget target, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FilePropertyEntry>> ComputeHashesAsync(
        string path, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FilePropertyEntry>> ReadSignatureAsync(
        string path, CancellationToken cancellationToken = default);
}

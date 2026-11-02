using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IRecycleBinService : IService
{
    Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default);
}

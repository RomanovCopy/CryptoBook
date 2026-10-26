using CryptoBook.AI;

namespace CryptoBook.Interfaces;

public interface IAiAttachmentService : IService
{
    Task<AiAttachment> ReadAsync(string path, CancellationToken cancellationToken = default);
}

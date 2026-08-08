using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IDocumentSaveEncryptionPolicy: IService
    {
        Task<DocumentSaveTarget?> ResolveAsync(
            DocumentSaveTarget target,
            bool sourceIsPlaintextFile);
    }
}

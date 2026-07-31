namespace CryptoBook.Interfaces
{
    public interface ICurrentDocumentSaver: IService
    {
        Task<bool> TrySaveCurrentAsync(
            CancellationToken cancellationToken = default);
    }
}

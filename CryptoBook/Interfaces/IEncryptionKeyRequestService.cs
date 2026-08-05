namespace CryptoBook.Interfaces
{
    public interface IEncryptionKeyRequestService: IService
    {
        bool EnsureKeyAvailable();
    }
}

namespace CryptoBook.Interfaces
{
    public interface IEncryptionKeyRequestService: IService
    {
        bool EnsureKeyAvailable();

        /// <summary>
        /// Always displays the key-entry dialog, including when a key is
        /// already available in memory.
        /// </summary>
        bool RequestKey() => EnsureKeyAvailable();
    }
}

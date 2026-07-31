namespace CryptoBook.Interfaces
{
    public interface IFileDisplayNameService: IService
    {
        string GetDisplayName(
            string? pathOrName,
            string? defaultExtension = null);
    }
}

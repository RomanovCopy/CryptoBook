namespace CryptoBook.Interfaces
{
    public interface IUriNavigationService: IService
    {
        bool TryOpen(Uri uri);
    }
}

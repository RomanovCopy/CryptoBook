namespace CryptoBook.Interfaces
{
    public interface IFavoriteDirectoryPathPolicy: IService
    {
        string Normalize(string path);
        string GetDefaultDisplayName(string normalizedPath);
        string GetDisplayPath(string normalizedPath);
        Task<bool> IsAvailableAsync(
            string normalizedPath,
            CancellationToken cancellationToken = default);
    }
}

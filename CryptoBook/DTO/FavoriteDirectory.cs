namespace CryptoBook.DTO
{
    public sealed record FavoriteDirectory(
        Guid Id,
        string Path,
        string DisplayName,
        int SortOrder);
}

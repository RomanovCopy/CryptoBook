namespace CryptoBook.DTO
{
    /// <summary>
    /// Безопасные метаданные недавно использованного документа.
    /// Содержимое документа и данные ключей в историю не записываются.
    /// </summary>
    public sealed record RecentDocument(
        string Path,
        DateTimeOffset LastAccessedAtUtc,
        int OpenCount);
}

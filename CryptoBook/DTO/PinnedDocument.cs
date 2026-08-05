namespace CryptoBook.DTO
{
    /// <summary>
    /// Безопасные метаданные закреплённого документа. Содержимое и ключи
    /// документа в хранилище Quick Access не попадают.
    /// </summary>
    public sealed record PinnedDocument(
        string Path,
        DateTimeOffset PinnedAtUtc,
        DateTimeOffset? LastOpenedAtUtc,
        int SortOrder);
}

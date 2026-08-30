namespace CryptoBook.DTO;

/// <summary>
/// Выбранный файл и необязательный снимок виртуального каталога, из которого
/// он был выбран.
/// </summary>
public sealed record MediaCatalogSelection(
    string SelectedPath,
    IReadOnlyList<string> FilePaths)
{
    public const string WindowContextKey = "mediaCatalogSelection";
}

namespace CryptoBook.DTO
{
    public sealed record ApplicationThemeOption(
        ApplicationTheme Theme,
        string DisplayName,
        string Description,
        string ResourceName,
        string BackgroundPreview,
        string AccentPreview);
}

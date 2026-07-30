namespace CryptoBook.Interfaces
{
    public interface IWindowsThemeProvider: IService
    {
        bool UsesLightTheme { get; }
        event EventHandler? ThemeChanged;
    }
}

using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IThemeManager: IService
    {
        IReadOnlyList<ApplicationThemeOption> AvailableThemes { get; }
        ApplicationTheme CurrentTheme { get; }

        void Initialize();
        void ApplyTheme(ApplicationTheme theme);
    }
}

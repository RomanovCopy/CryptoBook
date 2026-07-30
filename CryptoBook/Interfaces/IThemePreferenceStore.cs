using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IThemePreferenceStore: IService
    {
        ApplicationTheme Load();
        void Save(ApplicationTheme theme);
    }
}

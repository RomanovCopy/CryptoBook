using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class UserThemePreferenceStore: IThemePreferenceStore
    {
        public ApplicationTheme Load()
        {
            string stored = Properties.Settings.Default.CurrentTheme;
            if(Enum.TryParse(stored, ignoreCase: true, out ApplicationTheme theme))
                return theme;

            // Поддержка значений, сохранённых старой реализацией.
            const string legacySuffix = "Theme";
            if(stored.EndsWith(legacySuffix, StringComparison.OrdinalIgnoreCase) &&
               Enum.TryParse(
                   stored[..^legacySuffix.Length],
                   ignoreCase: true,
                   out theme))
            {
                return theme;
            }

            return ApplicationTheme.System;
        }

        public void Save(ApplicationTheme theme)
        {
            Properties.Settings.Default.CurrentTheme = theme.ToString();
            Properties.Settings.Default.Save();
        }
    }
}

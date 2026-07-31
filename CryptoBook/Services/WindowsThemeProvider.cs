using CryptoBook.Interfaces;

using Microsoft.Win32;

namespace CryptoBook.Services
{
    public sealed class WindowsThemeProvider:
        IWindowsThemeProvider,
        IDisposable
    {
        private const string PersonalizeKey =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AppsUseLightTheme = nameof(AppsUseLightTheme);

        public WindowsThemeProvider()
        {
            SystemEvents.UserPreferenceChanged +=
                OnUserPreferenceChanged;
        }

        public bool UsesLightTheme
        {
            get
            {
                using RegistryKey? key =
                    Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                object? value = key?.GetValue(AppsUseLightTheme);
                return value is not int mode || mode != 0;
            }
        }

        public event EventHandler? ThemeChanged;

        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -=
                OnUserPreferenceChanged;
        }

        private void OnUserPreferenceChanged(
            object sender,
            UserPreferenceChangedEventArgs args)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

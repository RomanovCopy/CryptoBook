using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows;

using Application = System.Windows.Application;

namespace CryptoBook.Infrastructure
{
    public sealed class ThemeManager:
        IThemeManager,
        IDisposable
    {
        private static readonly IReadOnlyList<ApplicationThemeOption> Themes =
        [
            new(
                ApplicationTheme.System,
                "Системная",
                "Использовать системные цвета Windows.",
                "SystemTheme",
                "#FFF3F3F3",
                "#FF0078D4"),
            new(
                ApplicationTheme.Light,
                "Светлая",
                "Светлый нейтральный интерфейс.",
                "LightTheme",
                "#FFF7F8FA",
                "#FF2563EB"),
            new(
                ApplicationTheme.Dark,
                "Тёмная",
                "Графитовый интерфейс для работы при слабом освещении.",
                "DarkTheme",
                "#FF202124",
                "#FF60A5FA"),
            new(
                ApplicationTheme.Sepia,
                "Сепия",
                "Тёплая палитра для продолжительного чтения.",
                "SepiaTheme",
                "#FFF2E8D5",
                "#FFA05A2C")
        ];

        private readonly Application app;
        private readonly IThemePreferenceStore preferenceStore;
        private readonly IWindowsThemeProvider windowsThemeProvider;
        private ResourceDictionary? activeThemeDictionary;

        public ThemeManager(
            Application app,
            IThemePreferenceStore preferenceStore,
            IWindowsThemeProvider windowsThemeProvider)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            this.preferenceStore = preferenceStore ??
                throw new ArgumentNullException(nameof(preferenceStore));
            this.windowsThemeProvider = windowsThemeProvider ??
                throw new ArgumentNullException(nameof(windowsThemeProvider));
            windowsThemeProvider.ThemeChanged += OnWindowsThemeChanged;
        }

        public IReadOnlyList<ApplicationThemeOption> AvailableThemes => Themes;

        public ApplicationTheme CurrentTheme { get; private set; } =
            ApplicationTheme.System;

        public void Initialize() =>
            ApplyThemeCore(preferenceStore.Load(), savePreference: false);

        public void ApplyTheme(ApplicationTheme theme) =>
            ApplyThemeCore(theme, savePreference: true);

        public void Dispose()
        {
            windowsThemeProvider.ThemeChanged -= OnWindowsThemeChanged;
        }

        private void ApplyThemeCore(
            ApplicationTheme theme,
            bool savePreference)
        {
            ApplicationThemeOption option = Themes.FirstOrDefault(
                item => item.Theme == theme) ?? Themes[0];
            string resourceName = option.Theme == ApplicationTheme.System
                ? windowsThemeProvider.UsesLightTheme
                    ? "LightTheme"
                    : "DarkTheme"
                : option.ResourceName;
            var uri = new Uri(
                $"/CryptoBook;component/Themes/{resourceName}.xaml",
                UriKind.Relative);
            var newTheme = new ResourceDictionary
            {
                Source = uri
            };

            Collection<ResourceDictionary> dictionaries =
                app.Resources.MergedDictionaries;
            int position = activeThemeDictionary is null
                ? FindThemeDictionaryIndex(dictionaries)
                : dictionaries.IndexOf(activeThemeDictionary);
            if(position >= 0)
                dictionaries.RemoveAt(position);
            else
                position = 0;

            dictionaries.Insert(
                Math.Min(position, dictionaries.Count),
                newTheme);
            activeThemeDictionary = newTheme;
            CurrentTheme = option.Theme;

            if(savePreference)
                preferenceStore.Save(CurrentTheme);
        }

        private void OnWindowsThemeChanged(
            object? sender,
            EventArgs args)
        {
            if(CurrentTheme != ApplicationTheme.System)
                return;

            if(app.Dispatcher.CheckAccess())
                ApplyThemeCore(ApplicationTheme.System, savePreference: false);
            else
            {
                app.Dispatcher.BeginInvoke(
                    new Action(() => ApplyThemeCore(
                        ApplicationTheme.System,
                        savePreference: false)));
            }
        }

        private static int FindThemeDictionaryIndex(
            Collection<ResourceDictionary> dictionaries)
        {
            for(int index = 0; index < dictionaries.Count; index++)
            {
                string? source =
                    dictionaries[index].Source?.OriginalString;
                if(source?.Contains(
                    "/Themes/",
                    StringComparison.OrdinalIgnoreCase) is true)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}

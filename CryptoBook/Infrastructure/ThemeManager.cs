using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace CryptoBook.Infrastructure
{
    public class ThemeManager: IThemeManager
    {
        private readonly Application _app;

        public ThemeManager(Application app)
        {
            _app = app;
        }

        public void ApplyTheme(string themeName)
        {
            var uri = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);
            var newTheme = new ResourceDictionary { Source = uri };

            // Удаляем старую тему, если она есть
            var existingTheme = _app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes") == true);
            if(existingTheme != null)
                _app.Resources.MergedDictionaries.Remove(existingTheme);

            // Добавляем новую тему
            _app.Resources.MergedDictionaries.Add(newTheme);
        }
    }
}

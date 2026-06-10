using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public sealed class ExtensionToIconConverter:IValueConverter
    {
        private readonly ISystemIconService _icons;
        public SystemIconSize Size { get; set; } = SystemIconSize.Small;

        public ExtensionToIconConverter(ISystemIconService icons) => _icons = icons;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var size = ParseSize(parameter) ?? Size;

            var ext = value switch
            {
                null => ".bin",
                string s when string.IsNullOrWhiteSpace(s) => ".bin",
                string s => s,
                _ => ".bin"
            };

            return _icons.GetIconForExtension(ext, size);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;

        private static SystemIconSize? ParseSize(object parameter)
        {
            if(parameter is null)
                return null;
            if(parameter is SystemIconSize s)
                return s;
            if(parameter is string str && Enum.TryParse<SystemIconSize>(str, true, out var parsed))
                return parsed;
            return null;
        }
    }
}

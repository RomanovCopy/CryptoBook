using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public sealed class PathToIconConverter:IValueConverter
    {

        private readonly ISystemIconService _icons;
        public SystemIconSize Size { get; set; } = SystemIconSize.Small;

        public PathToIconConverter(ISystemIconService icons) => _icons = icons;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // parameter может переопределять размер: "Small" / "Large"
            var size = ParseSize(parameter) ?? Size;

            if(value is not string path || string.IsNullOrWhiteSpace(path))
                return _icons.GetIconForExtension(".bin", size);

            if(IsDriveRoot(path))
                return _icons.GetIconForPath(path, size);

            // Если это директория (или предполагаем, что директория)
            // В TreeView часто путь может быть "реальным", но Directory.Exists может тормозить на сетевых путях.
            // Поэтому: если путь оканчивается "\" или "/", трактуем как папку, иначе по расширению.
            if(LooksLikeDirectory(path) || SafeDirectoryExists(path))
                return _icons.GetFolderIcon(size);

            var ext = Path.GetExtension(path);
            return _icons.GetIconForExtension(ext, size);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;

        private static bool LooksLikeDirectory(string path)
            => path.EndsWith("\\", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal);

        private static bool SafeDirectoryExists(string path)
        {
            try
            { return Directory.Exists(path); } catch { return false; }
        }

        private static SystemIconSize? ParseSize(object parameter)
        {
            if(parameter is null)
                return null;
            if(parameter is SystemIconSize s)
                return s;
            if(parameter is string str && Enum.TryParse<SystemIconSize>(str, ignoreCase: true, out var parsed))
                return parsed;
            return null;
        }

        private static bool IsDriveRoot(string path)
        => path.Length == 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
    }
}

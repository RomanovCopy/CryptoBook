using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public class BytesToGbConverter: IValueConverter
    {
        private static readonly string[] _sizes =
        { "B", "KB", "MB", "GB", "TB", "PB" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value == null)
                return "0 B";

            double bytes = value switch
            {
                byte b => b,
                short s => s,
                int i => i,
                long l => l,
                ushort us => us,
                uint ui => ui,
                ulong ul => ul,
                _ => throw new InvalidOperationException($"Unsupported type: {value.GetType()}")
            };

            if(bytes < 0)
                return "0 B";

            int order = 0;
            while(bytes >= 1024 && order < _sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }

            return bytes >= 10
                ? $"{bytes:0} {_sizes[order]}"
                : $"{bytes:0.##} {_sizes[order]}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

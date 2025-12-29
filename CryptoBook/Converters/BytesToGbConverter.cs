using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public class BytesToGbConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value is long bytes)
            {
                double gb = bytes / (1024.0 * 1024.0 * 1024.0);
                return gb;
            }
            return 0.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value is double gb)
            {
                long bytes = (long)(gb * 1024.0 * 1024.0 * 1024.0);
                return bytes;
            }
            return 0L;
        }
    }
}

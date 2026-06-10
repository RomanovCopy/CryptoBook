using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public sealed class TypeCheckConverter: IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if(value == null || parameter == null)
                return false;

            var target = parameter as Type;
            if(target == null)
                return false;

            return target.IsInstanceOfType(value);
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

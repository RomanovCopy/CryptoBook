using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public class FontSizeAdjustConverter: IValueConverter
    {
        /// <summary>
        /// уменьшение/увеличение размера шрифта
        /// </summary>
        /// <param name="value">первичный размер</param>
        /// <param name="targetType"></param>
        /// <param name="parameter">смещение</param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(double.TryParse((string)parameter, out double param))
            {
                if(value is double fontSize)
                {
                    value = fontSize + param;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // Обратное преобразование не требуется
        }
    }
}

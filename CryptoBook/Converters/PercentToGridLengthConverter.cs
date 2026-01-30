using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CryptoBook.Converters
{
/// <summary>
/// применяется для расчета GridLength колонок исходя из условия, что колонок три и центральная колонка занимает все 
/// оставшееся место после расчета левой и правой колонок
/// </summary>
    public class PercentToGridLengthConverter:IValueConverter
    {
        /// <summary>
        /// преобразование процента (0 - 1) от размера Grid в GridLength левой или правой колонки
        /// </summary>
        /// <param name="value">процент</param>
        /// <param name="targetType"></param>
        /// <param name="parameter">IFileExplorerViewModel</param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is double percent && parameter is IWindowOptions options)
            {
                // фактическая ширина Window
                double width = options.WindowWidth;
                if(width <= 0)
                    width = 1;

                return new GridLength(width * percent, GridUnitType.Pixel);
            }
            return new GridLength(100, GridUnitType.Pixel);
        }
        /// <summary>
        /// обратное преобразование
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is GridLength gl && parameter is IWindowOptions options)
            {
                double width = options.WindowWidth;
                if(width <= 0)
                    width = 1;

                return Math.Clamp(gl.Value / width, 0.05, 0.95);//защита от схлопывания в 0
            }
            return 0.3;
        }
    }
}

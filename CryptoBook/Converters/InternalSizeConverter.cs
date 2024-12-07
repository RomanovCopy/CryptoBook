using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public class InternalSizeConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double result = 0;

            if(parameter is string param && value is double val && App.Current.MainWindow is Window window)
            {
                // Получаем актуальные размеры окна
                double winHeight = window.ActualHeight;
                double winWidth = window.ActualWidth;

                result = param.ToLower() switch
                {
                    "width" => winWidth / 100 * val,
                    "height" => winHeight / 100 * val,
                    _ => 0
                };
            }

            return result > 0 ? result : DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double result = 0;

            if(parameter is string param && value is double val && App.Current.MainWindow is Window window)
            {
                // Получаем актуальные размеры окна
                double winHeight = window.ActualHeight;
                double winWidth = window.ActualWidth;

                result = param.ToLower() switch
                {
                    "width" => val / winWidth * 100,
                    "height" => val / winHeight * 100,
                    _ => 0
                };
            }

            return result > 0 ? result : DependencyProperty.UnsetValue;
        }
    }
}

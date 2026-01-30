using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;


namespace CryptoBook.Converters
{
    public class ColumnsWidthConverter: IValueConverter
    {
        /// <summary>
        /// сумма размеров всех колонок(служит для вычисления размера последней колонки)
        /// </summary>
        double sum = 0;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var window = (Window)System.Windows.Application.Current.MainWindow;
            try
            {
                if(parameter is string str && str == "last")
                {// последний размер формируется по остаточному принципу
                    double width = window.Width - sum;
                    sum = 0;
                    return width;
                } else
                {//преобразуем проценты в значение размера и суммируем с предыдущими размерами
                    double width = window.Width / 100 * (double)value;
                    sum += width;
                    return width;
                }
            } catch(Exception e) {  return null; }
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var window = (Window)System.Windows.Application.Current.MainWindow;
                //преобразуем значения в проценты
                return (double)value / window.Width * 100;
                //если обратное преобразование не требуется, возвращает пустое значение для свойста
                //return DependencyProperty.UnsetValu
            } catch(Exception e) { return null; }
        }

    }
}

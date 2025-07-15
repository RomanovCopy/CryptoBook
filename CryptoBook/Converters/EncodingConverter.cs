using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public class EncodingConverter: IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                string result = "";
                if(value is byte[] bytes && targetType == typeof(string))
                {
                    //result = view.HomeEncoding.GetString(bytes);
                }
                return result;
            } catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                byte[] result = [];
                if(value is string str && targetType == typeof(byte[]))
                {
                    //result = view.HomeEncoding.GetBytes(str);
                }
                return result;
            } catch
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}

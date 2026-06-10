using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

using Brush = System.Windows.Media.Brush;

namespace CryptoBook.Converters
{
    public class ColorToColorConverter: IValueConverter
    {


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                object result = null;
                if(value==null)return DependencyProperty.UnsetValue;
                var a = value.GetType();
                if(targetType == typeof(Brush) && value is System.Drawing.Color val)
                {
                    result = new SolidColorBrush(System.Windows.Media.Color.FromArgb(val.A, val.R, val.G, val.B));
                } else if(targetType == typeof(System.Drawing.Color) && value is Brush val1)
                {
                    var color = ((System.Windows.Media.SolidColorBrush)(val1)).Color;
                    result = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                } else
                {
                    return DependencyProperty.UnsetValue;
                }
                return result;
            } catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                object result;
                if(value == null)
                    return DependencyProperty.UnsetValue;
                if(targetType == typeof(Brush) && value is System.Drawing.Color val)
                {
                    result = new SolidColorBrush(System.Windows.Media.Color.FromArgb(val.A, val.R, val.G, val.B));
                } else if(targetType == typeof(System.Drawing.Color) && value is Brush val1)
                {
                    var color = ((System.Windows.Media.SolidColorBrush)(val1)).Color;
                    result = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                } else
                {
                    return DependencyProperty.UnsetValue;
                }
                return result;
            } catch
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}

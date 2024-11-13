using CryptoBook.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Reflection;
using Autofac;
using Brush = System.Windows.Media.Brush;
using System.Windows.Media;

namespace CryptoBook.Converters
{
    public class ColorToColorConverter: IValueConverter
    {

        private readonly ObservableCollection<System.Drawing.Color> MyColors;

        public ColorToColorConverter()
        {
            MyColors = [];
            foreach(PropertyInfo info in typeof(System.Drawing.Color).GetProperties())
            {
                if(info != null && info.PropertyType == typeof(System.Drawing.Color))
                {
                    var value = info.GetValue(null);
                    if(value != null)
                    {
                        MyColors.Add((System.Drawing.Color)value);
                    }
                }
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                object result = null;
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

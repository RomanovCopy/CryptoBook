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
    public class MediBrushSerializeConverter:IValueConverter
    {
        /// <summary>
        /// System.Windows.Media.Brush deserialize from string
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            object result = DependencyProperty.UnsetValue;
            if(value is string str && targetType == typeof(System.Windows.Media.Brush))
            {
                var stringReader = new System.IO.StringReader(str);
                var xmlReader = System.Xml.XmlReader.Create(stringReader);
                System.Windows.Media.Brush loadedBrush = (System.Windows.Media.Brush)System.Windows.Markup.XamlReader.Load(xmlReader);
                result = loadedBrush;
            }
            return result;
        }

        /// <summary>
        /// System.Windows.Media.Brush serialize to string
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            object result = DependencyProperty.UnsetValue;

            if(value is System.Windows.Media.Brush brush && targetType == typeof(string))
            {
                result = System.Windows.Markup.XamlWriter.Save(brush);
            }

            return result;
        }
    }
}

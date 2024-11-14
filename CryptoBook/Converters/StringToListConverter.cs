using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public class StringToListConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return TextList((string)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ListText((ObservableCollection<string>)value);
        }


        /// <summary>
        /// преобразование строки в коллекцию строк в соответствии с управляющими символами в её составе
        /// </summary>
        /// <param name="line">строка</param>
        /// <returns>коллекция строк</returns>
        public ObservableCollection<string> TextList(string line)
        {
            ObservableCollection<string> ls = [];
            if(line != null)
            {
                char[] separator = new char[] { '\r', '\n' };
                string[] l = line.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                foreach(string line1 in l)
                {
                    if(!string.IsNullOrEmpty(line1))
                    {
                        line1.Trim(new char[] { ' ', '\r', '\n' });
                        if(line1.Length > 0)
                            ls.Add(line1.Trim());
                    }
                }
            }
            return ls;
        }

        /// <summary>
        /// преобразование коллекции строк в строку
        /// </summary>
        /// <param name="list">коллекция строк</param>
        /// <returns>результирующая строка</returns>
        public string ListText(ObservableCollection<string> list)
        {
            string str = "";
            if(list != null && list.Count > 0)
                str = list.Aggregate((a, b) => $"{a}\r\n{b}");
            return str;
        }


    }
}

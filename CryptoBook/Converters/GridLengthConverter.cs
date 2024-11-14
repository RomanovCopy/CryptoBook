using System;
using System.Collections.Generic;
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
    public class GridLengthConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            GridLength result;
            try
            {
                if(value is double val && targetType == typeof(GridLength))
                {
                    switch(parameter.ToString())
                    {
                        case "NoteBookHeight":
                        {
                            result = new GridLength(System.Windows.Application.Current.MainWindow.ActualHeight / 100 * val);
                            break;
                        }
                    }
                }
                return result;
            } catch(Exception e)
            {
                ErrorWindow(e);
                return result;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double result=0;
            try
            {
                if(value is GridLength grid && targetType == typeof(double))
                {
                    switch(parameter.ToString())
                    {
                        case "NoteBookHeight":
                        {
                            result = grid.Value / System.Windows.Application.Current.MainWindow.ActualHeight * 100;
                            break;
                        }
                    }
                }
                return result;
            } catch(Exception e)
            {
                ErrorWindow(e);
                return result;
            }
        }


        private void ErrorWindow(Exception e, [CallerMemberName] string name = "")
        {
            var mytype = GetType().ToString().Split('.').LastOrDefault();
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            { System.Windows.MessageBox.Show(e.Message, $"{mytype}.{name}"); }));
        }

    }
}

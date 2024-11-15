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

using Autofac;


namespace CryptoBook.Converters
{
    public class SizeLocationConverter: IValueConverter
    {


        /// <summary>
        /// преобразует пришедшее от привязки значение в тот тип, который понимается приемником привязки
        /// </summary>
        /// <param name="value">значение, которое надо преобразовать</param>
        /// <param name="targetType"> тип, к которому надо преобразовать значение value</param>
        /// <param name="parameter">вспомогательный параметр</param>
        /// <param name="culture"> текущая культура приложения</param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double size = 0;
            try
            {
                if(value is double temp)
                {
                    switch(parameter.ToString())
                    {//размеры и положение окна задается в процентном отношении от разрешения основного манитора
                        case "width":
                        {
                            size = SystemParameters.FullPrimaryScreenWidth / 100 * temp;
                            break;
                        }
                        case "height":
                        {
                            size = SystemParameters.FullPrimaryScreenHeight / 100 * temp;
                            break;
                        }
                        case "top":
                        {
                            size = SystemParameters.FullPrimaryScreenHeight / 100 * temp;
                            break;
                        }
                        case "left":
                        {
                            size = SystemParameters.FullPrimaryScreenWidth / 100 * temp;
                            break;
                        }
                    }
                } else if(value is string str && parameter.ToString() == "state")
                {//задания режима отображения окна
                    switch(str)
                    {
                        case "Maximized":
                        {
                            return WindowState.Maximized;//полноэкранный режим
                        }
                        case "Normal":
                        {
                            return WindowState.Normal;//оконный режим
                        }
                        case "Minimized":
                        {
                            return WindowState.Minimized;//окно свернуто в панели задач
                        }
                    }
                }
                return size;
            } catch(Exception e)
            {
                ErrorWindow(e);
                return size;
            }
        }

        /// <summary>
        /// выполняет противоположную  Convert операцию.
        /// </summary>
        /// <param name="value">значение, которое надо преобразовать</param>
        /// <param name="targetType"> тип, к которому надо преобразовать значение value</param>
        /// <param name="parameter">вспомогательный параметр</param>
        /// <param name="culture"> текущая культура приложения</param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0;
            try
            {
                if(value is double temp)
                {
                    switch(parameter.ToString())
                    {
                        case "width":
                        {
                            percent = temp / SystemParameters.FullPrimaryScreenWidth * 100;
                            break;
                        }
                        case "height":
                        {
                            percent = temp / SystemParameters.FullPrimaryScreenHeight * 100;
                            break;
                        }
                        case "top":
                        {
                            percent = temp / SystemParameters.FullPrimaryScreenHeight * 100;
                            break;
                        }
                        case "left":
                        {
                            percent = temp / SystemParameters.FullPrimaryScreenWidth * 100;
                            break;
                        }
                    }
                } else if(value is WindowState str && parameter.ToString() == "state")
                {
                    return str.ToString();
                }
                return percent;

            } catch(Exception e)
            {
                ErrorWindow(e);
                return percent;
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

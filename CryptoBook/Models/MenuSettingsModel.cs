using CryptoBook.Infrastructure;

namespace CryptoBook.Models
{
    internal class MenuSettingsModel: ViewModelBase
    {
        public MenuSettingsModel()
        {
        }


        internal bool CanExecute_SetFontWeight(object? obj)
        {
            return true;
        }
        internal void Execute_SetFontWeight(object? obj)
        {
        }

        internal bool CanExecute_SetFontFamily(object? obj)
        {
            return true;
        }
        internal void Execute_SetFontFamily(object? obj)
        {
        }

        internal bool CanExecute_SetFontSize(object? obj)
        {
            return true;
        }
        internal void Execute_SetFontSize(object? obj)
        {
        }

        internal bool CanExecute_SetFontColor(object? obj)
        {
            return true;
        }
        internal void Execute_SetFontColor(object? obj)
        {
        }

        internal bool CanExecute_SetFontBackColor(object? obj)
        {
            return true;
        }
        internal void Execute_SetFontBackColor(object? obj)
        {
        }

        internal bool CanExecute_SetEncoding(object? obj)
        {
            return true;
        }
        internal void Execute_SetEncoding(object? obj)
        {
        }

        internal bool CanExecute_SetPaperColor(object? obj)
        {
            return true;
        }
        internal void Execute_SetPaperColor(object? obj)
        {
        }

        internal bool CanExecute_Localization(object? obj)
        {
            if(obj is string localize)
            {
                if(Thread.CurrentThread.CurrentUICulture.ToString() != localize)
                    return true;
            }
            return false;
        }
        internal void Execute_Localization(object? obj)
        {
            Properties.Settings.Default.CultureInfo = obj.ToString();
            Properties.Settings.Default.Save();
            App.Current.MainWindow.Close();
        }





        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
        }

        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
        }


        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
        }

    }
}

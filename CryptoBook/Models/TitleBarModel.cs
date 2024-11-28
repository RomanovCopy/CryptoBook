using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Media = System.Windows.Media;


namespace CryptoBook.Models
{
    internal class TitleBarModel: ViewModelBase
    {
        /// <summary>
        /// окно перемещается
        /// </summary>
        private bool _isDragging;

        /// <summary>
        /// высота TitleBar
        /// </summary>
        internal double MyFontSize { get => height; set => SetProperty(ref height, value); }
        double height;

        /// <summary>
        /// цвет шрифта и значков
        /// </summary>
        internal Media.Brush MyFontColor { get => fontColor; set => SetProperty(ref fontColor, value);}
        Media.Brush fontColor;

        /// <summary>
        /// цвет фона TitleBar
        /// </summary>
        internal Media.Brush MyBackColor { get => myBackColor; set => SetProperty(ref myBackColor, value); }
        Media.Brush myBackColor;

        /// <summary>
        /// текст внутри TitleBar
        /// </summary>
        internal string MyText { get => myText; set => SetProperty(ref myText, value); }
        string myText;



        internal TitleBarModel()
        {

        }

        internal bool CanExecute_Loaded(object obj)
        {
            return true;
        }
        internal void Execute_Loaded(object obj)
        {
            MyFontSize = 16;
            MyFontColor = Media.Brushes.Yellow;
            MyBackColor = Media.Brushes.Gray;
            MyText = "Romanov";
        }

        internal bool CanExecute_MouseLeftButtonDown(object obj)
        {
            return true;
        }
        internal void Execute_MouseLeftButtonDown(object obj)
        {
            if(!_isDragging)
            {
                _isDragging = true;
                App.Container.Resolve<MainWindow>().DragMove();
                _isDragging = false;
            }
        }

        internal bool CanExecute_TitleBarMouseMove(object obj)
        {
            return true;
        }
        internal void Execute_TitleBarMouseMove(object obj)
        {
            
        }

        internal bool CanExecute_ButtonBack_Click(object obj)
        {
            if(App.Container.IsRegistered<MainWindowViewModel>())
                return App.Container.Resolve<MainWindowViewModel>().FramelistGoBack.CanExecute(null);
            return false;
        }
        internal void Execute_ButtonBack_Click(object obj)
        {
            App.Container.Resolve<MainWindowViewModel>().FramelistGoBack.Execute(null);
        }

        internal bool CanExecute_ButtonForward_Click(object obj)
        {
            if(App.Container.IsRegistered<MainWindowViewModel>())
                return App.Container.Resolve<MainWindowViewModel>().FramelistGoForward.CanExecute(null);
            return false;
        }
        internal void Execute_ButtonForward_Click(object obj)
        {
            App.Container.Resolve<MainWindowViewModel>().FramelistGoForward.Execute(null);
        }

        internal bool CanExecute_ToggleMenu_Click(object obj)
        {
            return true;
        }
        internal void Execute_ToggleMenu_Click(object obj)
        {
            var container = (MainWindowViewModel)App.Container.Resolve<MainWindow>().DataContext;
            if(container!=null && container.ToggleMenuClick.CanExecute(null))
            {
                container.ToggleMenuClick.Execute(null);
            }
        }

        internal bool CanExecute_ButtonDarkThemeClick(object obj)
        {
            return false;
        }
        internal void Execute_ButtonDarkThemeClick(object obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ButtonSettingsClick(object obj)
        {
            return true;
        }
        internal void Execute_ButtonSettingsClick(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_ButtonLightTheme_Click(object obj)
        {
            return true;
        }
        internal void Execute_ButtonLightTheme_Click(object obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_MinButtonClick(object obj)
        {
            return true;
        }
        internal void Execute_MinButtonClick(object obj)
        {
        }

        internal bool CanExecute_MaxButtonClick(object obj)
        {
            return true;
        }
        internal void Execute_MaxButtonClick(object obj)
        {
            MyFontSize -= 1;
        }

        internal bool CanExecute_CloseButtonClick(object obj)
        {
            return true;
        }
        internal void Execute_CloseButtonClick(object obj)
        {
            MyFontSize += 1;
        }

        internal bool CanExecute_Closed(object obj)
        {
            return true;
        }
        internal void Execute_Closed(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_GoToWindow(object obj)
        {
            return false;
        }
        internal void Execute_GoToWindow(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_Closing(object obj)
        {
            return true;
        }
        internal void Execute_Closing(object obj)
        {
            throw new NotImplementedException();
        }

    }
}

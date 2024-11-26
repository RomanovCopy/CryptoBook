using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class TitleBarViewModel:ViewModelBase, ITitleBarViewModel
    {

        private readonly TitleBarModel titleBarModel;

        public double MyFontSize => titleBarModel.MyFontSize;

        public System.Windows.Media.Brush MyFontColor => titleBarModel.MyFontColor;

        

        public TitleBarViewModel()
        {
            titleBarModel = new();
            titleBarModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }





        public ICommand Loaded => loaded ??= 
            new RelayCommand(titleBarModel.Execute_Loaded, titleBarModel.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand MouseLeftButtonDown => 
            mouseLeftButtonDown ??= new RelayCommand(titleBarModel.Execute_MouseLeftButtonDown, titleBarModel.CanExecute_MouseLeftButtonDown);
        RelayCommand mouseLeftButtonDown;

        public ICommand TitleBarMouseMove => titleBarMouseMove ??= new RelayCommand(titleBarModel.Execute_TitleBarMouseMove, titleBarModel.CanExecute_TitleBarMouseMove);
        RelayCommand titleBarMouseMove;

        public ICommand ButtonBack_Click => buttonBack_Click ??= new RelayCommand(titleBarModel.Execute_ButtonBack_Click, titleBarModel.CanExecute_ButtonBack_Click);
        RelayCommand buttonBack_Click;

        public ICommand ButtonForward_Click => buttonForward_Click ??= new RelayCommand(titleBarModel.Execute_ButtonForward_Click, titleBarModel.CanExecute_ButtonForward_Click);
        RelayCommand buttonForward_Click;

        public ICommand ToggleMenu_Click => toggleMenu_Click ??= new RelayCommand(titleBarModel.Execute_ToggleMenu_Click, titleBarModel.CanExecute_ToggleMenu_Click);
        RelayCommand toggleMenu_Click;

        public ICommand ButtonLightTheme_Click => buttonLightTheme_Click ??= new RelayCommand(titleBarModel.Execute_ButtonLightTheme_Click, titleBarModel.CanExecute_ButtonLightTheme_Click);
        RelayCommand buttonLightTheme_Click;

        public ICommand ButtonDarkThemeClick => buttonDarkThemeClick ??= new RelayCommand(titleBarModel.Execute_ButtonDarkThemeClick, titleBarModel.CanExecute_ButtonDarkThemeClick);
        RelayCommand buttonDarkThemeClick;
        public ICommand ButtonSettingsClick => buttonSettingsClick ??= new RelayCommand(titleBarModel.Execute_ButtonSettingsClick, titleBarModel.CanExecute_ButtonSettingsClick);
        RelayCommand buttonSettingsClick;

        public ICommand MinButtonClick => minButtonClick ??= new RelayCommand(titleBarModel.Execute_MinButtonClick, titleBarModel.CanExecute_MinButtonClick);
        RelayCommand minButtonClick;

        public ICommand GoToWindow => goToWindow ??= new RelayCommand(titleBarModel.Execute_GoToWindow, titleBarModel.CanExecute_GoToWindow);
        RelayCommand goToWindow;

        public ICommand MaxButtonClick => maxButtonClick ??= new RelayCommand(titleBarModel.Execute_MaxButtonClick, titleBarModel.CanExecute_MaxButtonClick);
        RelayCommand maxButtonClick;

        public ICommand CloseButtonClick => closeButtonClick ??= new RelayCommand(titleBarModel.Execute_CloseButtonClick, titleBarModel.CanExecute_CloseButtonClick);
        RelayCommand closeButtonClick;

        public ICommand Closed => closed ??= new RelayCommand(titleBarModel.Execute_Closed, titleBarModel.CanExecute_Closed);
        RelayCommand closed;

        public ICommand Closing => closing ??= new RelayCommand(titleBarModel.Execute_Closing, titleBarModel.CanExecute_Closing);
        RelayCommand closing;

    }
}

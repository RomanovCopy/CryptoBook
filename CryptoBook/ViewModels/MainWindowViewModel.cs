using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MainWindowViewModel: ViewModelBase, IMainWindowViewModel, IWindowWithId, ICloseable
    {
        private readonly MainWindowModel mainWindowModel;
        private readonly IThemeManager themeManager;
        private readonly IWindowManager windowManager;

        public Guid WindowId => mainWindowModel.WindowId;

        public event EventHandler RequestClose;

        public bool IsMenuOpen { get => isMenuOpen; set => SetProperty(ref isMenuOpen, value); }
        bool isMenuOpen;

        public double WindowWidth { get => mainWindowModel.WindowWidth; set => mainWindowModel.WindowWidth = value; }
        public double WindowHeight { get => mainWindowModel.WindowHeight; set => mainWindowModel.WindowHeight = value; }
        public double WindowTop { get => mainWindowModel.WindowTop; set => mainWindowModel.WindowTop = value; }
        public double WindowLeft { get => mainWindowModel.WindowLeft; set => mainWindowModel.WindowLeft = value; }
        public WindowState WindowState { get => mainWindowModel.WindowState; set => mainWindowModel.WindowState = value; }


        public static Action Ready { get => MainWindowModel.Ready; set => MainWindowModel.Ready = value; }

        public MainWindowViewModel(ILifetimeScope scope)
        {
            IsMenuOpen = false;
            themeManager = scope.Resolve<IThemeManager>();
            windowManager = scope.Resolve<IWindowManager>();
            mainWindowModel = new(windowManager);
            mainWindowModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand SideMenuOpen => sideMenuOpen ??= new RelayCommand(mainWindowModel.Execute_SideMenuOpen, mainWindowModel.CanExecute_SideMenuOpen);
        RelayCommand sideMenuOpen;

        public ICommand SideMenuClose => sideMenuClose ??= new RelayCommand(mainWindowModel.Execute_SideMenuClose, mainWindowModel.CanExecute_SideMenuClose);
        RelayCommand sideMenuClose;

        public ICommand WindowPreviewMouseDown => windowPreviewMouseDown ??= new RelayCommand(mainWindowModel.Execute_WindowPreviewMouseDown, mainWindowModel.CanExecute_WindowPreviewMouseDown);
        RelayCommand windowPreviewMouseDown;


        public ICommand WindowToMinimize => windowToMinimize??=new RelayCommand(mainWindowModel.Execute_windowToMinimize, mainWindowModel.CanExecute_windowToMinimize);
        RelayCommand windowToMinimize;
        public ICommand WindowToMaximize => windowToMaximize ??= new RelayCommand(mainWindowModel.Execute_WindowToMaximize, mainWindowModel.CanExecute_WindowToMaximize);
        RelayCommand windowToMaximize;
        public ICommand WindowToNormal=>windowToNormal??=new RelayCommand(mainWindowModel.Execute_WindowToNormal, mainWindowModel.CanExecute_WindowToNormal);
        RelayCommand windowToNormal;


        public ICommand Loaded => loaded ??= new RelayCommand(mainWindowModel.Execute_Loaded, mainWindowModel.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(mainWindowModel.Execute_Close, mainWindowModel.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(mainWindowModel.Execute_Closing, mainWindowModel.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Closed => closed ??= new RelayCommand(mainWindowModel.Execute_Closed, mainWindowModel.CanExecute_Closed);
        private RelayCommand closed;


        public ICommand ToggleMenuCommand => toggleMenuCommand ??= new RelayCommand(Execute_ToggleMenuCommand);
        private RelayCommand toggleMenuCommand;

        private void Execute_ToggleMenuCommand(object? obj)
        {
            IsMenuOpen = !IsMenuOpen;
        }



    }
}

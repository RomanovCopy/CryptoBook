using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MainWindowViewModel: ViewModelBase, IMainWindowViewModel, ICloseable
    {
        private readonly IMainWindowModel mainWindowModel;

        public Guid WindowId => mainWindowModel.WindowId;

        public event EventHandler RequestClose;

        public bool IsMenuOpen { get => mainWindowModel.IsMenuOpen; set => mainWindowModel.IsMenuOpen = value; }

        public double WindowWidth { get => mainWindowModel.WindowWidth; set => mainWindowModel.WindowWidth = value; }
        public double WindowHeight { get => mainWindowModel.WindowHeight; set => mainWindowModel.WindowHeight = value; }
        public double WindowTop { get => mainWindowModel.WindowTop; set => mainWindowModel.WindowTop = value; }
        public double WindowLeft { get => mainWindowModel.WindowLeft; set => mainWindowModel.WindowLeft = value; }
        public WindowState WindowState { get => mainWindowModel.WindowState; set => mainWindowModel.WindowState = value; }


        public static Action Ready { get => MainWindowModel.Ready; set => MainWindowModel.Ready = value; }

        public MainWindowViewModel(IMainWindowModel mainWindowModel)
        {
            this.mainWindowModel = mainWindowModel ?? throw new ArgumentNullException(nameof(mainWindowModel));
            this.mainWindowModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand SideMenuClose => sideMenuClose ??= new RelayCommand(mainWindowModel.Execute_SideMenuClose, mainWindowModel.CanExecute_SideMenuClose);
        RelayCommand sideMenuClose;
        public ICommand WindowToMinimize => windowToMinimize ??= new RelayCommand(mainWindowModel.Execute_WindowToMinimize, mainWindowModel.CanExecute_WindowToMinimize);
        RelayCommand windowToMinimize;
        public ICommand WindowToMaximize => windowToMaximize ??= new RelayCommand(mainWindowModel.Execute_WindowToMaximize, mainWindowModel.CanExecute_WindowToMaximize);
        RelayCommand windowToMaximize;
        public ICommand WindowToNormal => windowToNormal ??= new RelayCommand(mainWindowModel.Execute_WindowToNormal, mainWindowModel.CanExecute_WindowToNormal);
        RelayCommand windowToNormal;
        public ICommand ToggleMenuCommand => toggleMenuCommand ??= new RelayCommand(mainWindowModel.Execute_ToggleMenuCommand, mainWindowModel.CanExecute_ToggleMenuCommand);
        private RelayCommand toggleMenuCommand;


        public ICommand Loaded => loaded ??= new RelayCommand(mainWindowModel.Execute_Loaded, mainWindowModel.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(mainWindowModel.Execute_Close, mainWindowModel.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(mainWindowModel.Execute_Closing, mainWindowModel.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Closed => closed ??= new RelayCommand(mainWindowModel.Execute_Closed, mainWindowModel.CanExecute_Closed);
        private RelayCommand closed;



    }



}

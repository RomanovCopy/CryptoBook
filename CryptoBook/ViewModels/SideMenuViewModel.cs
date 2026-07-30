using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using DTO = CryptoBook.DTO;

using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class SideMenuViewModel: ViewModelBase, ISideMenuViewModel
    {

        private readonly SideMenuModel sideMenuModel;

        public ObservableCollection<MenuItemBase> MenuItems { get => sideMenuModel.MenuItems; }
        public double Width { get => sideMenuModel.Width; set => sideMenuModel.Width = value; }
        public bool IsMenuOpen { get; set; }

        public double FontSizeHeader { get => sideMenuModel.FontSizeHeader; set => sideMenuModel.FontSizeHeader = value; }
        public double FontSize { get => sideMenuModel.FontSize; set => sideMenuModel.FontSize = value; }

        public SideMenuViewModel(ILifetimeScope scope)
        {
            sideMenuModel = new(scope);
            sideMenuModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand Loaded => loaded ??=
            new RelayCommand(sideMenuModel.Execute_Loaded, sideMenuModel.CanExecute_Lifecycle);
        RelayCommand loaded;

        public ICommand Close => close ??=
            new RelayCommand(sideMenuModel.Execute_Close, sideMenuModel.CanExecute_Lifecycle);
        RelayCommand close;

        public ICommand Closing => closing ??=
            new RelayCommand(sideMenuModel.Execute_Closing, sideMenuModel.CanExecute_Lifecycle);
        RelayCommand closing;

        public ICommand Closed => closed ??=
            new RelayCommand(sideMenuModel.Execute_Closed, sideMenuModel.CanExecute_Lifecycle);
        RelayCommand closed;
    }
}

using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class SideMenuViewModel: ViewModelBase, ISideMenuViewModel
    {

        private readonly SideMenuModel sideMenuModel;

        public ObservableCollection<MenuItemViewModel> MenuItems { get => sideMenuModel.MenuItems; }
        public double Width { get => sideMenuModel.Width; set => sideMenuModel.Width = value; }
        public bool IsMenuOpen { get; set; }

        public double FontSizeHeader { get => sideMenuModel.FontSizeHeader; set => sideMenuModel.FontSizeHeader = value; }
        public double FontSize { get => sideMenuModel.FontSize; set => sideMenuModel.FontSize = value; }

        public SideMenuViewModel(ILifetimeScope scope)
        {
            sideMenuModel = new(scope);
            sideMenuModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand Loaded { get; }

        public ICommand Close { get; }

        public ICommand Closing { get; }

        public ICommand Closed => throw new NotImplementedException();
    }
}

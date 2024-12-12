using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class SideMenuViewModel:ViewModelBase, ISideMenuViewModel
    {

        private readonly SideMenuModel sideMenuModel;

        public ObservableCollection<MenuItemViewModel> MenuItems { get => sideMenuModel.MenuItems; }
        public double Width { get => sideMenuModel.Width; set => sideMenuModel.Width=value; }

        public double FontSizeHeader { get => sideMenuModel.FontSizeHeader; set => sideMenuModel.FontSizeHeader = value; }
        public double FontSize { get => sideMenuModel.FontSize; set => sideMenuModel.FontSize = value; }

        public SideMenuViewModel()
        {
            sideMenuModel = new();
            sideMenuModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();
    }
}

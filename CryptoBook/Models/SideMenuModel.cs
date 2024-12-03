using CryptoBook.Infrastructure;
using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class SideMenuModel:ViewModelBase
    {
        internal ObservableCollection<MenuItemViewModel> MenuItems { get => menuItems; private set => SetProperty(ref menuItems, value); }
        ObservableCollection<MenuItemViewModel> menuItems;

        public SideMenuModel()
        {
            MenuItems = [];
            var menuitem = new MenuItemViewModel();
            menuitem.Name = "Home";
            menuitem.Children.Add(new MenuItemViewModel() { Name = "Romanov" });
            MenuItems.Add(menuitem);
        }
    }


}

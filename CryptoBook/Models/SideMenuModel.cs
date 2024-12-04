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
            MenuItems = InitializeMenu();
        }


        private ObservableCollection<MenuItemViewModel> InitializeMenu()
        {
           var MenuItems = new ObservableCollection<MenuItemViewModel>
        {
            new MenuItemViewModel
            {
                Name = "Dashboard",
                Icon = "📊",
                Children =
                {
                    new MenuItemViewModel { Name = "Statistics", Icon = "📈" },
                    new MenuItemViewModel { Name = "Reports", Icon = "📑" }
                }
            },
            new MenuItemViewModel
            {
                Name = "Settings",
                Icon = "⚙️",
                Children =
                {
                    new MenuItemViewModel { Name = "Profile", Icon = "👤"},
                    new MenuItemViewModel { Name = "Preferences", Icon = "🔧" }
                }
            }
        };
            return MenuItems;
        }

    }


}

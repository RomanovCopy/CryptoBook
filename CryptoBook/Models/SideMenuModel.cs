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

        internal double Width { get => width; set => SetProperty(ref width, value); }
        private double width;

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
                Name = "File",
                Icon = "",
                Children =
                {
                    new MenuItemViewModel { Name = "New", Icon = "📄" },
                    new MenuItemViewModel { Name = "Open", Icon = "📝" },
                    new MenuItemViewModel { Name = "Save", Icon = "💾" },
                    new MenuItemViewModel { Name = "Save As...", Icon = "💾" }
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

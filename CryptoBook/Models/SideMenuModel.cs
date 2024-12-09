using Autofac;

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
    public class SideMenuModel: ViewModelBase
    {
        internal ObservableCollection<MenuItemViewModel> MenuItems { get => menuItems; private set => SetProperty(ref menuItems, value); }
        ObservableCollection<MenuItemViewModel> menuItems;

        internal double Width { get => width; set => SetProperty(ref width, value); }
        private double width;

        private readonly MenuFileViewModel menuFileViewModel;

        public SideMenuModel()
        {
            menuFileViewModel = Locators.ViewModels.MenuFileViewModel;
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
                IsParrent=true,
                Children =
                {
                    new MenuItemViewModel { Name = "New", Icon = "📄", IsParrent=false, SelectItem=menuFileViewModel.NewFile },
                    new MenuItemViewModel { Name = "Open", Icon = "📝", IsParrent=false },
                    new MenuItemViewModel { Name = "Save", Icon = "💾", IsParrent=false },
                    new MenuItemViewModel { Name = "Save As...", Icon = "💾", IsParrent=false }
                }
            },
            new MenuItemViewModel
            {
                Name = "Settings",
                Icon = "",
                IsParrent=true,
                Children =
                {
                    new MenuItemViewModel { Name = "Profile", Icon = "👤", IsParrent=false},
                    new MenuItemViewModel { Name = "Preferences", Icon = "🔧", IsParrent=false }
                }
            }
        };
            return MenuItems;
        }

    }


}

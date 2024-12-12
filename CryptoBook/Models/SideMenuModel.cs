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

        /// <summary>
        /// ширина бокового меню в процентах от ширины окна
        /// </summary>
        internal double Width { get => width; set => SetProperty(ref width, value); }
        private double width;
        /// <summary>
        /// высота шрифта заголовков в процентах от вертикального разрешения экрана
        /// </summary>
        internal double FontSizeHeader { get => fontSizeHeader; set => SetProperty(ref fontSizeHeader, value); }
        double fontSizeHeader;
        /// <summary>
        /// высота шрифта в процентах от вертикального разрешения экрана
        /// </summary>
        internal double FontSize { get => fontSize; set => SetProperty(ref fontSize, value); }
        double fontSize;



        private readonly MenuFileViewModel menuFileViewModel;

        public SideMenuModel()
        {
            menuFileViewModel = Locators.ViewModels.MenuFileViewModel;
            Width = Properties.Settings.Default.SideMenuWidth;
            FontSizeHeader = Properties.Settings.Default.SideMenuFontSizeHeader;
            FontSize = Properties.Settings.Default.SideMenuFontSize;
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
                IsEnabled=true,
                FontSize=this.FontSize,
                Children =
                {
                    new MenuItemViewModel
                    {
                        Name = "New",
                        Icon = "📄",
                        IsParrent=false,
                        IsEnabled=true,
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.NewFile
                    },
                    new MenuItemViewModel
                    {
                        Name = "Open",
                        Icon = "📝",
                        IsParrent=false,
                        IsEnabled=true,
                        SelectItem=menuFileViewModel.OpenFile
                    },
                    new MenuItemViewModel
                    {
                        Name = "Save",
                        Icon = "💾",
                        IsParrent=false,
                        IsEnabled=false,
                        SelectItem=menuFileViewModel.SaveFile
                    },
                    new MenuItemViewModel
                    {
                        Name = "Save As...",
                        Icon = "💾",
                        IsParrent=false,
                        IsEnabled=true,
                        SelectItem=menuFileViewModel.SaveAsFile
                    }
                }
            },
            new MenuItemViewModel
            {
                Name = "Settings",
                Icon = "",
                IsParrent=true,
                Children =
                {
                    new MenuItemViewModel
                    {
                        Name = "Profile",
                        Icon = "👤",
                        IsParrent=false},
                    new MenuItemViewModel
                    {
                        Name = "Preferences",
                        Icon = "🔧",
                        IsParrent=false
                    }
                }
            }
        };
            return MenuItems;
        }

    }


}

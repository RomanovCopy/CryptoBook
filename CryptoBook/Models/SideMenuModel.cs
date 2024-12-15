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
        private readonly MenuSettingsViewModel menuSettingsViewModel;

        public SideMenuModel()
        {
            menuFileViewModel = Locators.ViewModels.MenuFileViewModel;
            menuSettingsViewModel = Locators.ViewModels.MenuSettingsViewModel;
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
                FontSize=this.FontSizeHeader,
                Children =
                {
                    new MenuItemViewModel
                    {
                        Name = "New file",
                        Icon = "📄",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.NewFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.NewFile
                    },
                    new MenuItemViewModel
                    {
                        Name = "Open File",
                        Icon = "📝",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.OpenFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.OpenFile
                    },
                    new MenuItemViewModel
                    {
                        Name = "Open directory",
                        Icon = "📂",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.SaveFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.SaveFile
                    },
                    new MenuItemViewModel
                    {
                        Name = "Save File",
                        Icon = "💾",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.SaveFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.SaveFile,
                    },
                    new MenuItemViewModel
                    {
                        Name = "Save File As...",
                        Icon = "🗂️",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.SaveAsFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.SaveAsFile
                    },
                    new MenuItemViewModel
                    {
                        Name = "Update File",
                        Icon = "🔄",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.UpdateFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.UpdateFile
                    },
                    new MenuItemViewModel
                    {
                        Name = "Synchronization",
                        Icon = "♻️",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.WorkingDirectorySynchronization.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.WorkingDirectorySynchronization,
                    },
                    new MenuItemViewModel
                    {
                        Name = "Close file",
                        Icon = "❌",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.CloseFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.CloseFile,
                    },
                }
            },
            new MenuItemViewModel
            {
                Name="Settings",
                Icon = "",
                IsParrent=true,
                IsEnabled=true,
                FontSize=this.FontSizeHeader,
                Children =
                {
                    new MenuItemViewModel
                    {
                        Name = "Ink",
                        Icon = "🎨",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuSettingsViewModel.SetFontColor.CanExecute(null),
                        SelectItem=menuSettingsViewModel.SetFontColor,
                    },
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

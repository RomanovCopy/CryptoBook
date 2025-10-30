using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Properties;
using CryptoBook.ViewModels;
using DTO=CryptoBook.DTO;

using System.Collections.ObjectModel;
using CryptoBook.DTO;

namespace CryptoBook.Models
{
    public class SideMenuModel: ViewModelBase
    {

        private readonly ILifetimeScope scope;
        internal ObservableCollection<MenuItemBase> MenuItems { get => menuItems; private set => SetProperty(ref menuItems, value); }
        ObservableCollection<MenuItemBase> menuItems;

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



        private readonly IMenuFileViewModel menuFileViewModel;
        private readonly IMenuSettingsViewModel menuSettingsViewModel;
        private readonly IMenuEncryptionViewModel menuEncryptionViewModel;
        private readonly IMenuContentViewModel menuContentViewModel;

        public SideMenuModel(ILifetimeScope _scope)
        {
            scope = _scope;
            menuFileViewModel = _scope.Resolve<IMenuFileViewModel>();
            menuSettingsViewModel = _scope.Resolve<IMenuSettingsViewModel>();
            menuEncryptionViewModel = _scope.Resolve<IMenuEncryptionViewModel>();
            menuContentViewModel = _scope.Resolve<IMenuContentViewModel>();
            Width = Properties.Settings.Default.SideMenuWidth;
            FontSizeHeader = Properties.Settings.Default.SideMenuFontSizeHeader;
            FontSize = Properties.Settings.Default.SideMenuFontSize;
            MenuItems = InitializeMenu();
        }


        private ObservableCollection<MenuItemBase> InitializeMenu()
        {
            var menuItems = new ObservableCollection<MenuItemBase>
            {
                new MenuFileItem(scope.Resolve<ICommandService>()),
            };
            return menuItems;
        }

    }


}

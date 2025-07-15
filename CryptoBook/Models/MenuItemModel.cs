using CryptoBook.Infrastructure;
using CryptoBook.ViewModels;

using System.Collections.ObjectModel;

namespace CryptoBook.Models
{
    internal class MenuItemModel: ViewModelBase
    {

        internal ObservableCollection<MenuItemViewModel> Children { get => children; private set => SetProperty(ref children, value); }
        ObservableCollection<MenuItemViewModel> children;

        internal string Name { get => name; set => SetProperty(ref name, value); }
        string name;

        internal string Icon { get => icon; set => SetProperty(ref icon, value); }
        string icon;

        internal bool IsParrent { get => isParrent; set => SetProperty(ref isParrent, value); }
        bool isParrent;

        internal bool IsExpanded { get => isExpanded; set => SetProperty(ref isExpanded, value); }
        bool isExpanded;

        internal bool IsEnabled { get => isEnable; set => SetProperty(ref isEnable, value); }
        bool isEnable;

        internal double FontSize { get => fontSize; set => SetProperty(ref fontSize, value); }
        double fontSize;


        public MenuItemModel()
        {
            Children = [];
        }



        internal bool CanExecute_SelectItem(object? obj)
        {
            return true;
        }
        internal void Execute_SelectItem(object? obj)
        {
        }

    }
}

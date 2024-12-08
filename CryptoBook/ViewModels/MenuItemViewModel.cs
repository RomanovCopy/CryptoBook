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
    public class MenuItemViewModel: ViewModelBase, IMenuItemViewModel
    {

        private readonly MenuItemModel menuItemModel;

        public ObservableCollection<MenuItemViewModel> Children { get => menuItemModel.Children; }

        public string Name { get => menuItemModel.Name; set => menuItemModel.Name = value; }

        public string Icon { get => menuItemModel.Icon; set => menuItemModel.Icon=value; }

        public bool IsParrent { get => menuItemModel.IsParrent; set => menuItemModel.IsParrent=value; }

        public bool IsExpanded { get => menuItemModel.IsExpanded; set => menuItemModel.IsExpanded=value; }


        public MenuItemViewModel()
        {
            menuItemModel = new();
            menuItemModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        public ICommand SelectItem => selectItem ??= new RelayCommand(menuItemModel.Execute_SelectItem, menuItemModel.CanExecute_SelectItem);
        RelayCommand selectItem;


        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();
    }
}

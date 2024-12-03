using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        internal bool IsExpanded { get => isExpanded; set => SetProperty(ref isExpanded, value); }
        bool isExpanded;


        public MenuItemModel()
        {
            Children = [];
        }



        internal bool CanExecute_SelectItem(object obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_SelectItem(object obj)
        {
            throw new NotImplementedException();
        }

    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.Infrastructure
{
    public class MenuItemBase:ViewModelBase
    {
        public string Name { get=>_name; set=>SetProperty(ref _name, value); }
        string _name;
        public bool IsEnabled { get=>_enabled; set=>SetProperty(ref _enabled, value); }
        bool _enabled;
        public ICommand Command { get=>_command; set=>SetProperty(ref _command, value); }
        ICommand _command;
        public bool HasChildren => Children.Count > 0;
        public int Order { get => _order; set => SetProperty(ref _order, value);}
        int _order;

        public ObservableCollection<MenuItemBase> Children { get; private set; }

        public MenuItemBase()
        {
            Children = [];
            Initialize();
        }

        protected virtual void Initialize()
        {

        }

    }
}

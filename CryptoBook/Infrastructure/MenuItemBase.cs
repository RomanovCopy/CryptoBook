using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using DTO = CryptoBook.DTO;

namespace CryptoBook.Infrastructure
{
    public class MenuItemBase:ViewModelBase
    {
        protected readonly ICommandService commandService;
        public string Name { get=>_name; set=>SetProperty(ref _name, value); }
        string _name;
        public bool IsEnabled { get=>_enabled; set=>SetProperty(ref _enabled, value); }
        bool _enabled;
        public ICommand Command { get=>_command; set=>SetProperty(ref _command, value); }
        ICommand _command;
        public bool HasChildren { get => hasChildren; set => SetProperty(ref hasChildren, value); }
        bool hasChildren;
        public int Order { get => _order; set => SetProperty(ref _order, value);}
        int _order;

        public ObservableCollection<DTO.MenuItem> Children { get; private set; }

        public MenuItemBase(ICommandService commandService)
        {
            Children = [];
            this.commandService = commandService;
        }

        protected virtual void Initialize()
        {

        }

    }
}

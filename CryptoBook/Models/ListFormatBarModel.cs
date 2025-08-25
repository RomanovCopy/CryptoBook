using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    internal class ListFormatBarModel: ViewModelBase
    {
        private readonly IListService service;

        public ListFormatBarModel(IListService service)
        {
            this.service = service;
        }


        internal bool CanExecute_ToggleBulleted(object? obj)
        {
            return service.CanToggle;
        }
        internal void Execute_ToggleBulleted(object? obj)
        {
            service.ToggleBulleted();
        }


        internal bool CanExecute_ToggleNumbered(object? obj)
        {
            return service.CanToggle;
        }
        internal void Execute_ToggleNumbered(object? obj)
        {
            service.ToggleNumbered();
        }


        internal bool CanExecute_ClearLists(object? obj)
        {
            return service.CanClear;
        }
        internal void Execute_ClearLists(object? obj)
        {
            service.ClearLists();
        }
    }
}

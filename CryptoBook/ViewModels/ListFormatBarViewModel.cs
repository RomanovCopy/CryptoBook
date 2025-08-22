using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class ListFormatBarViewModel:ViewModelBase,IListFormatBarViewModel
    {
        private readonly ListFormatBarModel model;

        public ListFormatBarViewModel(IListService service)
        {
            model = new ListFormatBarModel(service);
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand ToggleBulleted => toggleBulleted ??= new RelayCommand(model.Execute_ToggleBulleted, model.CanExecute_ToggleBulleted);
        RelayCommand toggleBulleted;

        public ICommand ToggleNumbered => toggleBulleted ??= new RelayCommand(model.Execute_ToggleNumbered, model.CanExecute_ToggleNumbered);
        RelayCommand toggleNumbered;

        public ICommand ClearLists => clearLists ??= new RelayCommand(model.Execute_ClearLists, model.CanExecute_ClearLists);
        RelayCommand clearLists;

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}

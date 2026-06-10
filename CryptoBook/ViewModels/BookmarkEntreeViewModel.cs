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
    public class BookmarkEntryViewModel: ViewModelBase, IBookmarkEntryViewModel
    {
        private readonly BookmarkEntryModel model;

        public string Name { get => model.Name; set => model.Name = value; }
        public string Note { get => model.Note; set => model.Note = value; }
        public Uri? BookmarkUri { get => model.BookmarkUri; set => model.BookmarkUri = value; }
        public string BookmarkUriString => model.BookmarkUriString;

        public BookmarkEntryViewModel()
        {
            model = new BookmarkEntryModel();
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand Loaded => loaded ??= new RelayCommand(model.Execute_Loaded, model.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(model.Execute_Close, model.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(model.Execute_Closing, model.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Closed => closed ??= new RelayCommand(model.Execute_Closed, model.CanExecute_Closed);
        RelayCommand closed;
    }
}

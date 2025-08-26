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
    public class BookmarkEntryViewModel:ViewModelBase, IViewModel
    {
        private readonly BookmarkEntryModel model;

        public string Name { get => model.Name; set => model.Name = value; }
        public string Note { get=>model.Note; set => model.Note = value; }
        public Uri BookmarkUri => model.BookmarkUri;

        public BookmarkEntryViewModel()
        {
            model=new BookmarkEntryModel();
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}

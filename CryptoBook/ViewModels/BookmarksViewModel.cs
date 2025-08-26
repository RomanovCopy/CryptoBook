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
    public class BookmarksViewModel: ViewModelBase, IBookmarksViewModel
    {
        private readonly BookmarksModel model;

        public ObservableCollection<BookmarkEntryViewModel> Bookmarks => throw new NotImplementedException();


        public BookmarksViewModel(IRichTextBoxService service, IBookmarkService bookmarkService)
        {
            model=new BookmarksModel(service, bookmarkService);
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand AddAtCaret => throw new NotImplementedException();

        public ICommand Remove => throw new NotImplementedException();

        public ICommand Rename => throw new NotImplementedException();

        public ICommand NavigateTo => throw new NotImplementedException();

        public ICommand InsertHyperlinkTo => throw new NotImplementedException();

        public ICommand RebuildIndexFromDocument => throw new NotImplementedException();

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}

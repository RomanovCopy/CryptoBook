using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class BookmarksViewModel: ViewModelBase, IBookmarksViewModel
    {
        private readonly BookmarksModel model;
        private IBookmarkService bookmarkService;

        public ObservableCollection<BookmarkEntryViewModel> Bookmarks => bookmarkService.Bookmarks;


        public BookmarksViewModel(IRichTextBoxService service, IBookmarkService bookmarkService, 
            IBookmarkValidationService bookmarkValidationService, IWindowManager windowManager)
        {
            model = new BookmarksModel(service, bookmarkService, bookmarkValidationService, windowManager);
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            this.bookmarkService = bookmarkService;
            this.bookmarkService.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand AddAtCaret => addAtCaret ??= new RelayCommand(model.Execute_AddAtCaret, model.CanExecute_AddAtCaret);
        RelayCommand addAtCaret;
        public ICommand NextBookmark => nextBookmark ??= new RelayCommand(model.Execute_NextBookmark, model.CanExecute_NextBookmark);
        RelayCommand nextBookmark;

        public ICommand PreviousBookmark => previousBookmark ??= new RelayCommand(model.Execute_PreviousBookmark, model.CanExecute_PreviousBookmark);
        RelayCommand previousBookmark;

        public ICommand Remove => remove??= new RelayCommand(model.Execute_Remove, model.CanExecute_Remove);
        RelayCommand remove;

        public ICommand Rename => rename ??= new RelayCommand(model.Execute_Rename, model.CanExecute_Rename);
        RelayCommand rename;

        public ICommand NavigateTo => navigateTo ??= new RelayCommand(model.Execute_NavigateTo, model.CanExecute_NavigateTo);
        RelayCommand navigateTo;

        public ICommand InsertHyperlinkTo => insertHyperlinkTo ??= new RelayCommand(model.Execute_InsertHyperlinkTo, model.CanExecute_InsertHyperlinkTo);
        RelayCommand insertHyperlinkTo;

        public ICommand RebuildIndexFromDocument => rebuildIndexFromDocument ??= new RelayCommand(model.Execute_RebuildIndexFromDocument, model.CanExecute_RebuildIndexFromDocument);
        RelayCommand rebuildIndexFromDocument;

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

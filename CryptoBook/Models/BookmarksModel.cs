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
    internal class BookmarksModel:ViewModelBase
    {
        private readonly IRichTextBoxService service;
        private readonly IBookmarkService bookmarkService;

        internal ObservableCollection<BookmarkEntryViewModel> Bookmarks=>bookmarkService.Bookmarks;

        public BookmarksModel(IRichTextBoxService service, IBookmarkService bookmarkService)
        {
            this.service = service;
            this.bookmarkService = bookmarkService;
        }

    }
}

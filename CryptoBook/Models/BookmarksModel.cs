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


        public BookmarksModel(IRichTextBoxService service, IBookmarkService bookmarkService)
        {
            this.service = service;
            this.bookmarkService = bookmarkService;
        }



        internal bool CanExecute_AddAtCaret(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_AddAtCaret(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Remove(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Remove(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Rename(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Rename(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_NavigateTo(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_NavigateTo(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_InsertHyperlinkTo(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_InsertHyperlinkTo(object? obj)
        {
            throw new NotImplementedException();
        }



        internal bool CanExecute_RebuildIndexFromDocument(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_RebuildIndexFromDocument(object? obj)
        {
            throw new NotImplementedException();
        }



        internal bool CanExecute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
    }
}

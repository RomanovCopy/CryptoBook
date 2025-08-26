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
        private readonly IBookmarkValidationService bookmarkValidationService;


        public BookmarksModel(IRichTextBoxService service, IBookmarkService bookmarkService, 
            IBookmarkValidationService bookmarkValidationService)
        {
            this.service = service;
            this.bookmarkValidationService = bookmarkValidationService;
            this.bookmarkService = bookmarkService;
        }



        internal bool CanExecute_AddAtCaret(object? obj)
        {
            return bookmarkValidationService.CanInsertBookmark(service,null) == ValidationResult.Success;
        }
        internal void Execute_AddAtCaret(object? obj)
        {

        }


        internal bool CanExecute_Remove(object? obj)
        {
            return bookmarkValidationService.CanRemoveBookmark == ValidationResult.Success;
        }
        internal void Execute_Remove(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Rename(object? obj)
        {
            return bookmarkValidationService.CanRenameBookmark == ValidationResult.Success;
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
            return bookmarkValidationService.CanInsertHyperlink == ValidationResult.Success;  
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

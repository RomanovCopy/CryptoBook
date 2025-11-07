using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Models
{
    internal class BookmarksModel:ViewModelBase
    {
        private readonly IRichTextBoxService service;
        private readonly IBookmarkService bookmarkService;
        private readonly IBookmarkValidationService bookmarkValidationService;
        private readonly IWindowManager windowManager;



        public BookmarksModel(IRichTextBoxService service, IBookmarkService bookmarkService, 
            IBookmarkValidationService bookmarkValidationService, IWindowManager windowManager)
        {
            this.service = service;
            this.bookmarkValidationService = bookmarkValidationService;
            this.bookmarkService = bookmarkService;
            this.windowManager = windowManager;
        }



        internal bool CanExecute_AddAtCaret(object? obj)
        {
            return true;
        }
        internal void Execute_AddAtCaret(object? obj)
        {
            var win = windowManager.CreateWindow<Views.BookmarksEditor>();
            windowManager.ShowWindow<Views.BookmarksEditor>(((IWindowWithId)win.DataContext).WindowId);
        }

        internal bool CanExecute_PreviousBookmark(object? obj)
        {
            return true;
        }
        internal void Execute_PreviousBookmark(object? obj)
        {

        }


        internal bool CanExecute_NextBookmark(object? obj)
        {
            return true;
        }
        internal void Execute_NextBookmark(object? obj)
        {
        }



        internal bool CanExecute_Remove(object? obj)
        {
            return true;
        }
        internal void Execute_Remove(object? obj)
        {
        }


        internal bool CanExecute_Rename(object? obj)
        {
            return true;
        }
        internal void Execute_Rename(object? obj)
        {
        }


        internal bool CanExecute_NavigateTo(object? obj)
        {
            return true;
        }
        internal void Execute_NavigateTo(object? obj)
        {
        }


        internal bool CanExecute_InsertHyperlinkTo(object? obj)
        {
            return true;
        }
        internal void Execute_InsertHyperlinkTo(object? obj)
        {
        }



        internal bool CanExecute_RebuildIndexFromDocument(object? obj)
        {
            return true;
        }
        internal void Execute_RebuildIndexFromDocument(object? obj)
        {
        }



        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
        }


        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
        }


        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
        }


        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        internal void Execute_Closed(object? obj)
        {
        }

    }
}

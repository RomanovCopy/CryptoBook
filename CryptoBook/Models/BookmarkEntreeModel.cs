using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    internal class BookmarkEntryModel: ViewModelBase
    {
        internal string Name
        {
            get => name;
            set => SetProperty(ref name, value ?? string.Empty);
        }
        string name = string.Empty;
        internal string Note
        {
            get => note;
            set => SetProperty(ref note, value ?? string.Empty);
        }
        string note = string.Empty;
        internal Uri? BookmarkUri 
        {
            get => bookmarkUri;
            set 
            { 
                if(value is Uri uri)
                {
                    SetProperty(ref bookmarkUri, uri);
                    BookmarkUriString = uri.ToString();
                }
                else
                {
                    SetProperty(ref bookmarkUri, null);
                    BookmarkUriString = string.Empty;
                }
            }
        }
        Uri? bookmarkUri;
        internal string BookmarkUriString
        {
            get => bookmarkUriString;
            private set => SetProperty(ref bookmarkUriString, value ?? string.Empty);
        }
        string bookmarkUriString = string.Empty;


        internal BookmarkEntryModel()
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

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
        internal string Name { get => name is null ? "No data" : name; set => SetProperty(ref name, value is null ? "No data" : value ); }
        string name;
        internal string Note { get => note is null ? "No data" : note; set => SetProperty(ref note, value is null ? "No data" : value); }
        string note;
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
                    BookmarkUriString = Uri.UnescapeDataString("No data");
                }
            }
        }
        Uri? bookmarkUri;
        internal string BookmarkUriString { get => bookmarkUriString is null ? "No data" : bookmarkUriString; 
            private set => SetProperty(ref bookmarkUriString, value is null ? "No data" : value); }
        string bookmarkUriString;


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

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
        internal string Name { get => name; set => SetProperty(ref name, value); }
        string name;
        internal string Note { get => note is null ? string.Empty : note; set => SetProperty(ref note, value); }
        string? note;
        internal Uri BookmarkUri { get => bookmarkUri; set => SetProperty(ref bookmarkUri, value); }
        Uri bookmarkUri;

        internal BookmarkEntryModel()
        {
        }
    }
}

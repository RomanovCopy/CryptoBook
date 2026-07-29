using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IBookmarksEditorViewModel: IViewModel, IWindowWithId, ICloseable
    {
        double Width { get; set; }
        double Height { get; set; }
        double WindowTop { get; set; }
        double WindowLeft { get; set; }
        WindowState WindowState { get; set; }


        ObservableCollection<IBookmarkEntryViewModel> Bookmarks { get; }

        /// <summary>
        /// выбранная закладка (для привязки в UI)
        /// </summary>
        IBookmarkEntryViewModel? SelectedBookmark { get; set; }
        string RenameTo { get; set; }
        string LinkText { get; set; }
        string StatusMessage { get; }

        ICommand NavigateTo { get; }
        ICommand Remove { get; }
        ICommand Rename { get; }
        ICommand InsertHyperlinkTo { get; }
        ICommand RebuildIndexFromDocument { get; }
    }
}

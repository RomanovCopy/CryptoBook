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


        ObservableCollection<BookmarkEntryViewModel> Bookmarks { get; }

        /// <summary>
        /// выбранная закладка (для привязки в UI)
        /// </summary>
        BookmarkEntryViewModel? SelctedBookmark { get; set; }

        /// <summary>
        /// обработка изменения выделения в списке закладок
        /// </summary>
        ICommand SelectionChangedBookmarks { get; }
    }
}

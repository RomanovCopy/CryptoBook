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
    public interface IBookmarksViewModel:IViewModel, ICloseable
    {
        /// <summary>
        /// Публично доступное хранилище закладок
        /// </summary>
        ObservableCollection<IBookmarkEntryViewModel> Bookmarks { get; }


        /// <summary>
        /// Добавить закладку в позицию каретки с именем пользователя
        /// </summary>
        ICommand AddAtCaret { get; }
        /// <summary>
        /// перейти к следующей закладке
        /// </summary>
        ICommand NextBookmark { get; }
        /// <summary>
        /// переход к передыдущей закладке
        /// </summary>
        ICommand PreviousBookmark { get; }
        /// <summary>
        /// Удалить закладку
        /// </summary>
        ICommand Remove{ get; }
        /// <summary>
        /// Переименовать закладку (и в документе, и в словаре)
        /// </summary>
        ICommand Rename{ get; }
        /// <summary>
        /// Перейти к закладке
        /// </summary>
        ICommand NavigateTo{ get; }
        /// <summary>
        /// Вставить Hyperlink на закладку в текущую позицию (видимый текст задаётся параметром)
        /// </summary>
        ICommand InsertHyperlinkTo{ get; }
        /// <summary>
        /// Перестроить словарь из реально существующих якорей документа (например, после загрузки XAML)
        /// </summary>
        ICommand RebuildIndexFromDocument{ get; }

    }
}

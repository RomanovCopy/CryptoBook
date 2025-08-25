using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IBookmarksViewModel:IViewModel
    {
        /// <summary>
        /// Публично доступное хранилище закладок
        /// </summary>
        Dictionary<string, Uri> Bookmarks { get; }

        /// <summary>
        /// Добавить закладку в позицию каретки с именем пользователя
        /// </summary>
        ICommand AddAtCaret{ get; }
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

using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IBookmarkService:INotifyPropertyChanged, IService
    {
        /// <summary>
        /// Публично доступное хранилище закладок
        /// </summary>
        ObservableCollection<IBookmarkEntryViewModel> Bookmarks { get; }


        /// <summary>
        /// закладка с таким именем уже существует?
        /// </summary>
        bool Exists(string name);

        /// <summary>
        /// Добавить закладку в позицию каретки с именем пользователя
        /// </summary>
        void AddAtCaret(IRichTextBoxService service, string name);
        /// <summary>
        /// Удалить закладку
        /// </summary>
        bool Remove(IRichTextBoxService service, string name);
        /// <summary>
        /// Переименовать закладку (и в документе, и в словаре)
        /// </summary>
        void Rename(IRichTextBoxService service, string oldName, string newName);
        /// <summary>
        /// Перейти к закладке
        /// </summary>
        bool NavigateTo(IRichTextBoxService service, string name);
        /// <summary>
        /// Вставить Hyperlink на закладку в текущую позицию (видимый текст задаётся параметром)
        /// </summary>
        void InsertHyperlinkTo(IRichTextBoxService service, string bookmarkName, string? linkText = null);
        /// <summary>
        /// Перестроить словарь из реально существующих якорей документа (например, после загрузки XAML)
        /// </summary>
        void RebuildIndexFromDocument(IRichTextBoxService service);

    }
}

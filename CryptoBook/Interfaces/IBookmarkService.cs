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

        /// <summary>Имя или заметка изменились без события TextChanged редактора.</summary>
        event EventHandler? MetadataChanged;

        /// <summary>Переход выполнен в исходном документе; синхронизирует видимый предпросмотр.</summary>
        event EventHandler<Uri>? Navigated;


        /// <summary>
        /// закладка с таким именем уже существует?
        /// </summary>
        bool Exists(string name);

        /// <summary>
        /// Вставить закладку с собственным текстом (именем) в позицию каретки без выделения
        /// </summary>
        void AddAtCaret(IRichTextBoxService service, string name);
        /// <summary>
        /// Удалить закладку вместе с её содержимым и всеми ссылками на неё, включая их подписи
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
        /// <summary>Перейти по внутренней ссылке в исходном документе.</summary>
        bool NavigateToUri(Uri uri);
        bool NavigateNext(IRichTextBoxService service);
        bool NavigatePrevious(IRichTextBoxService service);
        /// <summary>
        /// Вставить Hyperlink с собственным текстом в позицию каретки без выделения
        /// </summary>
        void InsertHyperlinkTo(IRichTextBoxService service, string bookmarkName, string? linkText = null);
        /// <summary>
        /// Перестроить словарь из реально существующих якорей документа (например, после загрузки XAML)
        /// </summary>
        void RebuildIndexFromDocument(IRichTextBoxService service);

    }
}

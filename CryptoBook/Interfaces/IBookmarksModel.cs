using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Описывает состояние и прикладные операции подсистемы закладок.
    /// Не содержит WPF-команд и не зависит от представления.
    /// </summary>
    public interface IBookmarksModel: INotifyPropertyChanged
    {
        ObservableCollection<IBookmarkEntryViewModel> Bookmarks { get; }

        IBookmarkEntryViewModel? SelectedBookmark { get; set; }
        string NewBookmarkName { get; set; }
        string RenameTo { get; set; }
        string LinkText { get; set; }
        string StatusMessage { get; }

        bool CanAddAtCaret();
        void AddAtCaret();

        bool CanNavigateNext();
        void NavigateNext();

        bool CanNavigatePrevious();
        void NavigatePrevious();

        bool CanRemove(IBookmarkEntryViewModel? bookmark);
        void Remove(IBookmarkEntryViewModel? bookmark);

        bool CanRename(IBookmarkEntryViewModel? bookmark);
        void Rename(IBookmarkEntryViewModel? bookmark);

        bool CanNavigateTo(IBookmarkEntryViewModel? bookmark);
        void NavigateTo(IBookmarkEntryViewModel? bookmark);

        bool CanInsertHyperlink(IBookmarkEntryViewModel? bookmark);
        void InsertHyperlink(IBookmarkEntryViewModel? bookmark);

        bool CanRebuildIndex();
        void RebuildIndex();
    }
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Состояние и сценарии отдельного диспетчера закладок.
    /// Не содержит WPF-команд и не зависит от ViewModel.
    /// </summary>
    public interface IBookmarksEditorModel: INotifyPropertyChanged, IWindowWithId
    {
        double Width { get; set; }
        double Height { get; set; }
        double WindowTop { get; set; }
        double WindowLeft { get; set; }
        WindowState WindowState { get; set; }

        ObservableCollection<IBookmarkEntryViewModel> Bookmarks { get; }
        IBookmarkEntryViewModel? SelectedBookmark { get; set; }
        string RenameTo { get; set; }
        string LinkText { get; set; }
        string StatusMessage { get; }

        bool CanNavigateTo(IBookmarkEntryViewModel? bookmark);
        void NavigateTo(IBookmarkEntryViewModel? bookmark);
        bool CanRemove(IBookmarkEntryViewModel? bookmark);
        void Remove(IBookmarkEntryViewModel? bookmark);
        bool CanRename(IBookmarkEntryViewModel? bookmark);
        void Rename(IBookmarkEntryViewModel? bookmark);
        bool CanInsertHyperlink(IBookmarkEntryViewModel? bookmark);
        void InsertHyperlink(IBookmarkEntryViewModel? bookmark);
        bool CanRebuildIndex();
        void RebuildIndex();

        void Load();
        void Close();
        void Closing();
        void Closed();
    }
}

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace CryptoBook.Models
{
    /// <summary>
    /// Управляет состоянием окна диспетчера и делегирует операции общей
    /// модели закладок. Представление и ICommand здесь отсутствуют.
    /// </summary>
    public sealed class BookmarksEditorModel:
        ViewModelBase,
        IBookmarksEditorModel
    {
        private const double MinimumWidth = 560;
        private const double MinimumHeight = 360;

        private readonly IBookmarksModel bookmarks;
        private readonly IWindowManager windowManager;

        private double width;
        private double height;
        private double windowTop;
        private double windowLeft;
        private WindowState windowState;

        public Guid WindowId { get; } = Guid.NewGuid();

        public double Width { get => width; set => SetProperty(ref width, value); }
        public double Height { get => height; set => SetProperty(ref height, value); }
        public double WindowTop
        {
            get => windowTop;
            set => SetProperty(ref windowTop, value);
        }
        public double WindowLeft
        {
            get => windowLeft;
            set => SetProperty(ref windowLeft, value);
        }
        public WindowState WindowState
        {
            get => windowState;
            set => SetProperty(ref windowState, value);
        }

        public ObservableCollection<IBookmarkEntryViewModel> Bookmarks =>
            bookmarks.Bookmarks;

        public IBookmarkEntryViewModel? SelectedBookmark
        {
            get => bookmarks.SelectedBookmark;
            set => bookmarks.SelectedBookmark = value;
        }

        public string RenameTo
        {
            get => bookmarks.RenameTo;
            set => bookmarks.RenameTo = value;
        }

        public string LinkText
        {
            get => bookmarks.LinkText;
            set => bookmarks.LinkText = value;
        }

        public string StatusMessage => bookmarks.StatusMessage;

        public BookmarksEditorModel(
            IBookmarksModel bookmarks,
            IWindowManager windowManager)
        {
            this.bookmarks = bookmarks ??
                throw new ArgumentNullException(nameof(bookmarks));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));

            bookmarks.PropertyChanged += (_, args) =>
                OnPropertyChanged(args.PropertyName ?? string.Empty);
            RestoreWindowSettings();
        }

        public bool CanNavigateTo(IBookmarkEntryViewModel? bookmark) =>
            bookmarks.CanNavigateTo(bookmark);

        public void NavigateTo(IBookmarkEntryViewModel? bookmark) =>
            bookmarks.NavigateTo(bookmark);

        public bool CanRemove(IBookmarkEntryViewModel? bookmark) =>
            bookmarks.CanRemove(bookmark);

        public void Remove(IBookmarkEntryViewModel? bookmark) =>
            bookmarks.Remove(bookmark);

        public bool CanRename(IBookmarkEntryViewModel? bookmark) =>
            bookmarks.CanRename(bookmark);

        public void Rename(IBookmarkEntryViewModel? bookmark) =>
            bookmarks.Rename(bookmark);

        public bool CanInsertHyperlink(IBookmarkEntryViewModel? bookmark) =>
            bookmarks.CanInsertHyperlink(bookmark);

        public void InsertHyperlink(IBookmarkEntryViewModel? bookmark) =>
            bookmarks.InsertHyperlink(bookmark);

        public bool CanRebuildIndex() => bookmarks.CanRebuildIndex();

        public void RebuildIndex() => bookmarks.RebuildIndex();

        public void Load() => RebuildIndex();

        public void Close() => windowManager.CloseWindow(WindowId);

        public void Closing() => SaveWindowSettings();

        public void Closed()
        {
        }

        private void RestoreWindowSettings()
        {
            Width = Math.Max(
                MinimumWidth,
                Properties.Settings.Default.BookmarksEditor_Width);
            Height = Math.Max(
                MinimumHeight,
                Properties.Settings.Default.BookmarksEditor_Height);
            WindowTop = Properties.Settings.Default.BookmarksEditor_Top;
            WindowLeft = Properties.Settings.Default.BookmarksEditor_Left;
            WindowState = Enum.TryParse<WindowState>(
                Properties.Settings.Default.BookmarksEditor_State,
                out var state)
                ? state
                : WindowState.Normal;
        }

        private void SaveWindowSettings()
        {
            if(WindowState == WindowState.Normal)
            {
                Properties.Settings.Default.BookmarksEditor_Width = Width;
                Properties.Settings.Default.BookmarksEditor_Height = Height;
                Properties.Settings.Default.BookmarksEditor_Left = WindowLeft;
                Properties.Settings.Default.BookmarksEditor_Top = WindowTop;
            }

            Properties.Settings.Default.BookmarksEditor_State =
                WindowState.ToString();
            Properties.Settings.Default.Save();
        }
    }
}

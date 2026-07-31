using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    /// <summary>
    /// WPF-адаптер диспетчера закладок. Состояние, операции и сохранение
    /// параметров окна реализованы в <see cref="IBookmarksEditorModel"/>.
    /// </summary>
    public sealed class BookmarksEditorViewModel:
        ViewModelBase,
        IBookmarksEditorViewModel
    {
        private readonly IBookmarksEditorModel model;

        event EventHandler ICloseable.RequestClose
        {
            add { }
            remove { }
        }

        public Guid WindowId => model.WindowId;

        public double Width
        {
            get => model.Width;
            set => model.Width = value;
        }

        public double Height
        {
            get => model.Height;
            set => model.Height = value;
        }

        public double WindowTop
        {
            get => model.WindowTop;
            set => model.WindowTop = value;
        }

        public double WindowLeft
        {
            get => model.WindowLeft;
            set => model.WindowLeft = value;
        }

        public WindowState WindowState
        {
            get => model.WindowState;
            set => model.WindowState = value;
        }

        public ObservableCollection<IBookmarkEntryViewModel> Bookmarks =>
            model.Bookmarks;

        public IBookmarkEntryViewModel? SelectedBookmark
        {
            get => model.SelectedBookmark;
            set => model.SelectedBookmark = value;
        }

        public string RenameTo
        {
            get => model.RenameTo;
            set => model.RenameTo = value;
        }

        public string LinkText
        {
            get => model.LinkText;
            set => model.LinkText = value;
        }

        public string StatusMessage => model.StatusMessage;

        public BookmarksEditorViewModel(IBookmarksEditorModel model)
        {
            this.model = model ??
                throw new ArgumentNullException(nameof(model));
            model.PropertyChanged += (_, args) =>
                OnPropertyChanged(args.PropertyName ?? string.Empty);
        }

        public ICommand NavigateTo => navigateTo ??=
            new RelayCommand(
                parameter => model.NavigateTo(parameter as IBookmarkEntryViewModel),
                parameter => model.CanNavigateTo(parameter as IBookmarkEntryViewModel));
        private RelayCommand? navigateTo;

        public ICommand Remove => remove ??=
            new RelayCommand(
                parameter => model.Remove(parameter as IBookmarkEntryViewModel),
                parameter => model.CanRemove(parameter as IBookmarkEntryViewModel));
        private RelayCommand? remove;

        public ICommand Rename => rename ??=
            new RelayCommand(
                parameter => model.Rename(parameter as IBookmarkEntryViewModel),
                parameter => model.CanRename(parameter as IBookmarkEntryViewModel));
        private RelayCommand? rename;

        public ICommand InsertHyperlinkTo => insertHyperlinkTo ??=
            new RelayCommand(
                parameter => model.InsertHyperlink(parameter as IBookmarkEntryViewModel),
                parameter => model.CanInsertHyperlink(parameter as IBookmarkEntryViewModel));
        private RelayCommand? insertHyperlinkTo;

        public ICommand RebuildIndexFromDocument => rebuildIndexFromDocument ??=
            new RelayCommand(_ => model.RebuildIndex(), _ => model.CanRebuildIndex());
        private RelayCommand? rebuildIndexFromDocument;

        public ICommand Loaded => loaded ??=
            new RelayCommand(_ => model.Load(), _ => model.CanRebuildIndex());
        private RelayCommand? loaded;

        public ICommand Close => close ??=
            new RelayCommand(_ => model.Close());
        private RelayCommand? close;

        public ICommand Closing => closing ??=
            new RelayCommand(_ => model.Closing());
        private RelayCommand? closing;

        public ICommand Closed => closed ??=
            new RelayCommand(_ => model.Closed());
        private RelayCommand? closed;
    }
}

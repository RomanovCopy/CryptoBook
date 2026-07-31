using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    /// <summary>
    /// Представляет модель закладок в WPF: создаёт команды и открывает менеджер.
    /// Прикладные операции и валидация находятся в <see cref="IBookmarksModel"/>.
    /// </summary>
    public sealed class BookmarksViewModel: ViewModelBase, IBookmarksViewModel
    {
        private readonly IBookmarksModel model;
        private readonly IWindowManager windowManager;

        event EventHandler ICloseable.RequestClose
        {
            add { }
            remove { }
        }

        public ObservableCollection<IBookmarkEntryViewModel> Bookmarks =>
            model.Bookmarks;

        public IBookmarkEntryViewModel? SelectedBookmark
        {
            get => model.SelectedBookmark;
            set => model.SelectedBookmark = value;
        }

        public string NewBookmarkName
        {
            get => model.NewBookmarkName;
            set => model.NewBookmarkName = value;
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

        public BookmarksViewModel(
            IBookmarksModel model,
            IWindowManager windowManager)
        {
            this.model = model ??
                throw new ArgumentNullException(nameof(model));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));

            model.PropertyChanged += (_, args) =>
                OnPropertyChanged(args.PropertyName ?? string.Empty);
        }

        public ICommand AddAtCaret => addAtCaret ??=
            new RelayCommand(_ => model.AddAtCaret(), _ => model.CanAddAtCaret());
        private RelayCommand? addAtCaret;

        public ICommand NextBookmark => nextBookmark ??=
            new RelayCommand(_ => model.NavigateNext(), _ => model.CanNavigateNext());
        private RelayCommand? nextBookmark;

        public ICommand PreviousBookmark => previousBookmark ??=
            new RelayCommand(
                _ => model.NavigatePrevious(),
                _ => model.CanNavigatePrevious());
        private RelayCommand? previousBookmark;

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

        public ICommand NavigateTo => navigateTo ??=
            new RelayCommand(
                parameter => model.NavigateTo(parameter as IBookmarkEntryViewModel),
                parameter => model.CanNavigateTo(parameter as IBookmarkEntryViewModel));
        private RelayCommand? navigateTo;

        public ICommand InsertHyperlinkTo => insertHyperlinkTo ??=
            new RelayCommand(
                parameter => model.InsertHyperlink(parameter as IBookmarkEntryViewModel),
                parameter => model.CanInsertHyperlink(parameter as IBookmarkEntryViewModel));
        private RelayCommand? insertHyperlinkTo;

        public ICommand RebuildIndexFromDocument => rebuildIndexFromDocument ??=
            new RelayCommand(_ => model.RebuildIndex(), _ => model.CanRebuildIndex());
        private RelayCommand? rebuildIndexFromDocument;

        public ICommand OpenManager => openManager ??=
            new RelayCommand(_ =>
            {
                var id = windowManager.CreateWindow<BookmarksEditor>();
                windowManager.ShowWindow(id);
            });
        private RelayCommand? openManager;

        public ICommand Loaded => loaded ??=
            new RelayCommand(_ => model.RebuildIndex(), _ => model.CanRebuildIndex());
        private RelayCommand? loaded;

        public ICommand Close => NoOpCommand;
        public ICommand Closing => NoOpCommand;
        public ICommand Closed => NoOpCommand;

        private static ICommand NoOpCommand { get; } = new RelayCommand(_ => { });
    }
}

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    /// <summary>
    /// Представляет модель закладок в WPF: создаёт команды и открывает менеджер.
    /// Прикладные операции и валидация находятся в <see cref="IBookmarksModel"/>.
    /// </summary>
    public sealed class BookmarksViewModel: ViewModelBase, IBookmarksViewModel, IDisposable
    {
        private readonly IBookmarksModel model;
        private readonly IWindowManager windowManager;
        private readonly IRichtextboxViewModel? documentView;

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
        public bool IsBookmarkCreationEnabled => documentView?.IsPreviewMode != true;
        public bool IsLinkInsertionEnabled => documentView?.IsPreviewMode != true;

        public BookmarksViewModel(
            IBookmarksModel model,
            IWindowManager windowManager,
            IRichtextboxViewModel? documentView = null)
        {
            this.model = model ??
                throw new ArgumentNullException(nameof(model));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            this.documentView = documentView;

            model.PropertyChanged += OnModelChanged;
            if(documentView != null)
                documentView.PropertyChanged += OnDocumentViewChanged;
        }

        private void OnModelChanged(object? sender, PropertyChangedEventArgs args)
        {
            OnPropertyChanged(args.PropertyName ?? string.Empty);
            if(args.PropertyName is nameof(NewBookmarkName) or nameof(Bookmarks))
                addAtCaret?.RaiseCanExecuteChanged();
            if(args.PropertyName == nameof(LinkText))
                insertHyperlinkTo?.RaiseCanExecuteChanged();
            if(args.PropertyName is nameof(SelectedBookmark) or nameof(Bookmarks))
            {
                navigateTo?.RaiseCanExecuteChanged();
                remove?.RaiseCanExecuteChanged();
                rename?.RaiseCanExecuteChanged();
                insertHyperlinkTo?.RaiseCanExecuteChanged();
                nextBookmark?.RaiseCanExecuteChanged();
                previousBookmark?.RaiseCanExecuteChanged();
            }
        }

        private void OnDocumentViewChanged(object? sender, PropertyChangedEventArgs args)
        {
            if(args.PropertyName != nameof(IRichtextboxViewModel.IsPreviewMode))
                return;
            OnPropertyChanged(nameof(IsBookmarkCreationEnabled), nameof(IsLinkInsertionEnabled));
            addAtCaret?.RaiseCanExecuteChanged();
            insertHyperlinkTo?.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            model.PropertyChanged -= OnModelChanged;
            if(documentView != null)
                documentView.PropertyChanged -= OnDocumentViewChanged;
        }

        public ICommand AddAtCaret => addAtCaret ??=
            new RelayCommand(_ =>
            {
                if(IsBookmarkCreationEnabled && model.CanAddAtCaret())
                    model.AddAtCaret();
            }, _ => IsBookmarkCreationEnabled && model.CanAddAtCaret());
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
                parameter =>
                {
                    var bookmark = parameter as IBookmarkEntryViewModel;
                    if(IsLinkInsertionEnabled && model.CanInsertHyperlink(bookmark))
                        model.InsertHyperlink(bookmark);
                },
                parameter => IsLinkInsertionEnabled &&
                    model.CanInsertHyperlink(parameter as IBookmarkEntryViewModel));
        private RelayCommand? insertHyperlinkTo;

        public ICommand RebuildIndexFromDocument => rebuildIndexFromDocument ??=
            new RelayCommand(_ => model.RebuildIndex(), _ => model.CanRebuildIndex());
        private RelayCommand? rebuildIndexFromDocument;

        public ICommand OpenManager => openManager ??=
            new RelayCommand(_ =>
            {
                var id = windowManager.CreateWindow<BookmarksEditor>(new Dictionary<string, object?> {
                    [BookmarksEditorViewModel.BookmarksContextKey] = model,
                    [BookmarksEditorViewModel.DocumentViewContextKey] = documentView
                });
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

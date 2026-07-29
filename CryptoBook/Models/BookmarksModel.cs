using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.ObjectModel;

namespace CryptoBook.Models
{
    /// <summary>
    /// Координирует бизнес-сценарии закладок: проверку, изменение документа
    /// и состояние текущей операции. WPF-команды создаёт ViewModel.
    /// </summary>
    public sealed class BookmarksModel: ViewModelBase, IBookmarksModel
    {
        private readonly IRichTextBoxService richTextBox;
        private readonly IBookmarkService bookmarkService;
        private readonly IBookmarkValidationService validation;

        private IBookmarkEntryViewModel? selectedBookmark;
        private string newBookmarkName = string.Empty;
        private string renameTo = string.Empty;
        private string linkText = string.Empty;
        private string statusMessage = string.Empty;

        public ObservableCollection<IBookmarkEntryViewModel> Bookmarks =>
            bookmarkService.Bookmarks;

        public IBookmarkEntryViewModel? SelectedBookmark
        {
            get => selectedBookmark;
            set
            {
                if(!SetProperty(ref selectedBookmark, value))
                    return;

                RenameTo = value?.Name ?? string.Empty;
                StatusMessage = string.Empty;
            }
        }

        public string NewBookmarkName
        {
            get => newBookmarkName;
            set => SetProperty(ref newBookmarkName, value ?? string.Empty);
        }

        public string RenameTo
        {
            get => renameTo;
            set => SetProperty(ref renameTo, value ?? string.Empty);
        }

        public string LinkText
        {
            get => linkText;
            set => SetProperty(ref linkText, value ?? string.Empty);
        }

        public string StatusMessage
        {
            get => statusMessage;
            private set => SetProperty(ref statusMessage, value);
        }

        public BookmarksModel(
            IRichTextBoxService richTextBox,
            IBookmarkService bookmarkService,
            IBookmarkValidationService validation)
        {
            this.richTextBox = richTextBox ??
                throw new ArgumentNullException(nameof(richTextBox));
            this.bookmarkService = bookmarkService ??
                throw new ArgumentNullException(nameof(bookmarkService));
            this.validation = validation ??
                throw new ArgumentNullException(nameof(validation));

            bookmarkService.PropertyChanged += (_, _) =>
                OnPropertyChanged(nameof(Bookmarks));
        }

        public bool CanAddAtCaret() =>
            validation.CanInsertBookmark(
                richTextBox,
                NewBookmarkName,
                bookmarkService.Exists).Ok;

        public void AddAtCaret() =>
            Execute(() =>
            {
                var name = NewBookmarkName.Trim();
                bookmarkService.AddAtCaret(richTextBox, name);
                SelectedBookmark = bookmarkService.Bookmarks[^1];
                NewBookmarkName = string.Empty;
                StatusMessage = $"Закладка «{name}» добавлена.";
                return true;
            });

        public bool CanNavigateNext() => Bookmarks.Count > 0;

        public void NavigateNext() =>
            Execute(() => bookmarkService.NavigateNext(richTextBox));

        public bool CanNavigatePrevious() => Bookmarks.Count > 0;

        public void NavigatePrevious() =>
            Execute(() => bookmarkService.NavigatePrevious(richTextBox));

        public bool CanRemove(IBookmarkEntryViewModel? bookmark)
        {
            bookmark = ResolveBookmark(bookmark);
            return bookmark != null &&
                validation.CanRemoveBookmark(richTextBox, bookmark.Name).Ok;
        }

        public void Remove(IBookmarkEntryViewModel? bookmark)
        {
            bookmark = ResolveBookmark(bookmark);
            if(bookmark == null)
                return;

            Execute(() =>
            {
                var name = bookmark.Name;
                var removed = bookmarkService.Remove(richTextBox, name);
                if(removed)
                {
                    SelectedBookmark = null;
                    StatusMessage = $"Закладка «{name}» удалена.";
                }
                return removed;
            });
        }

        public bool CanRename(IBookmarkEntryViewModel? bookmark)
        {
            bookmark = ResolveBookmark(bookmark);
            return bookmark != null &&
                validation.CanRenameBookmark(
                    richTextBox,
                    bookmark.Name,
                    RenameTo,
                    bookmarkService.Exists).Ok;
        }

        public void Rename(IBookmarkEntryViewModel? bookmark)
        {
            bookmark = ResolveBookmark(bookmark);
            if(bookmark == null)
                return;

            Execute(() =>
            {
                var oldName = bookmark.Name;
                var newName = RenameTo.Trim();
                bookmarkService.Rename(richTextBox, oldName, newName);
                StatusMessage = $"«{oldName}» переименована в «{newName}».";
                return true;
            });
        }

        public bool CanNavigateTo(IBookmarkEntryViewModel? bookmark)
        {
            bookmark = ResolveBookmark(bookmark);
            return bookmark != null &&
                validation.CanNavigateTo(richTextBox, bookmark.Name).Ok;
        }

        public void NavigateTo(IBookmarkEntryViewModel? bookmark)
        {
            bookmark = ResolveBookmark(bookmark);
            if(bookmark != null)
                Execute(() => bookmarkService.NavigateTo(richTextBox, bookmark.Name));
        }

        public bool CanInsertHyperlink(IBookmarkEntryViewModel? bookmark)
        {
            bookmark = ResolveBookmark(bookmark);
            return bookmark != null &&
                validation.CanNavigateTo(richTextBox, bookmark.Name).Ok &&
                validation.CanInsertHyperlink(richTextBox, LinkText).Ok;
        }

        public void InsertHyperlink(IBookmarkEntryViewModel? bookmark)
        {
            bookmark = ResolveBookmark(bookmark);
            if(bookmark == null)
                return;

            Execute(() =>
            {
                bookmarkService.InsertHyperlinkTo(
                    richTextBox,
                    bookmark.Name,
                    LinkText);
                LinkText = string.Empty;
                StatusMessage = $"Ссылка на «{bookmark.Name}» вставлена.";
                return true;
            });
        }

        public bool CanRebuildIndex() =>
            validation.CanRebuildIndexFromDocument(richTextBox).Ok;

        public void RebuildIndex() =>
            Execute(() =>
            {
                bookmarkService.RebuildIndexFromDocument(richTextBox);
                SelectedBookmark = null;
                StatusMessage = $"Индекс обновлён. Закладок: {Bookmarks.Count}.";
                return true;
            });

        private IBookmarkEntryViewModel? ResolveBookmark(
            IBookmarkEntryViewModel? bookmark) =>
            bookmark ?? SelectedBookmark;

        private void Execute(Func<bool> action)
        {
            try
            {
                if(!action() && string.IsNullOrEmpty(StatusMessage))
                    StatusMessage = "Операцию выполнить не удалось.";
            }
            catch(Exception exception)
            {
                StatusMessage = exception.Message;
            }
        }
    }
}

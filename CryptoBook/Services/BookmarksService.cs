using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    /// <summary>
    /// Хранит пользовательские данные закладок и синхронизирует их с якорями FlowDocument.
    /// </summary>
    public sealed class BookmarksService: ViewModelBase, IBookmarkService
    {
        private const string MetadataPrefix = "CryptoBook.Bookmark:";

        private readonly IRichTextBoxService source;

        private readonly Dictionary<string, BookmarkRecord> index =
            new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<IBookmarkEntryViewModel> Bookmarks { get; } = [];

        public event EventHandler? MetadataChanged;
        public event EventHandler<Uri>? Navigated;

        public BookmarksService(IRichTextBoxService service)
        {
            source = service ?? throw new ArgumentNullException(nameof(service));
        }

        public bool Exists(string name) =>
            !string.IsNullOrWhiteSpace(name) && index.ContainsKey(name.Trim());

        public void AddAtCaret(IRichTextBoxService service, string name)
        {
            ArgumentNullException.ThrowIfNull(service);
            if(!service.Selection.IsEmpty)
                throw new InvalidOperationException(LocalizationManager.GetString("Bookmarks.SelectionNotAllowed"));
            name = NormalizeName(name);
            EnsureValidNewName(name);

            var anchorId = $"Bookmark_{Guid.NewGuid():N}";
            var position = GetInsertionPosition(service);

            service.BeginChange();
            try
            {
                var anchor = new Span(position, position)
                {
                    Name = anchorId
                };
                anchor.Inlines.Add(new Run(name));
                MoveCaretAfterSpan(service, anchor);

                var record = CreateRecord(service, name, string.Empty, anchorId);
                anchor.Tag = SerializeMetadata(record.Entry);
                AddRecord(name, record);
            }
            finally
            {
                service.EndChange();
            }
        }

        public bool Remove(IRichTextBoxService service, string name)
        {
            ArgumentNullException.ThrowIfNull(service);
            if(string.IsNullOrWhiteSpace(name) || !index.TryGetValue(name.Trim(), out var record))
                return false;

            service.BeginChange();
            try
            {
                var anchors = EnumerateNamedSpans(service.Document).ToList();
                var anchor = anchors.FirstOrDefault(span => span.Name == record.AnchorId);
                var removedIds = new HashSet<string>(StringComparer.Ordinal) { record.AnchorId };
                var removedElements = new HashSet<Span>();
                if(anchor != null)
                    removedElements.Add(anchor);
                var links = new FlowDocumentWalker().Find<Hyperlink>(service.Document).ToList();
                // Старые документы могут содержать закладки внутри якоря или подписи
                // ссылки. Удаляем и их ссылки, не оставляя записей на удалённый текст.
                bool expanded;
                do
                {
                    foreach(var link in links.Where(link =>
                        link.NavigateUri is { IsAbsoluteUri: false } uri &&
                        removedIds.Contains(Uri.UnescapeDataString(uri.OriginalString.TrimStart('#')))))
                    {
                        removedElements.Add(link);
                    }
                    expanded = false;
                    foreach(var nested in anchors.Where(span =>
                        removedElements.Any(parent => ContainsSpan(parent, span))))
                    {
                        expanded |= removedIds.Add(nested.Name);
                    }
                } while(expanded);

                var removedRecords = index.Values.Where(item => removedIds.Contains(item.AnchorId)).ToList();
                var roots = removedElements.Where(span => !removedElements.Any(parent =>
                    parent != span && ContainsSpan(parent, span))).ToList();
                foreach(var element in roots)
                    element.SiblingInlines?.Remove(element);
                foreach(var removed in removedRecords)
                    RemoveRecord(removed);
            }
            finally
            {
                service.EndChange();
            }

            return true;
        }

        public void Rename(IRichTextBoxService service, string oldName, string newName)
        {
            ArgumentNullException.ThrowIfNull(service);
            oldName = oldName?.Trim() ?? string.Empty;
            newName = NormalizeName(newName);

            if(!index.TryGetValue(oldName, out var record))
                throw new KeyNotFoundException($"Нет закладки «{oldName}».");
            if(string.Equals(record.Entry.Name, newName, StringComparison.Ordinal))
                return;

            if(!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                EnsureValidNewName(newName);
            index.Remove(oldName);
            record.Entry.Name = newName;
            index[newName] = record;
            UpdateAnchorMetadata(service, record);
            OnPropertyChanged(nameof(Bookmarks));
        }

        public bool NavigateTo(IRichTextBoxService service, string name)
        {
            ArgumentNullException.ThrowIfNull(service);
            if(string.IsNullOrWhiteSpace(name) || !index.TryGetValue(name.Trim(), out var record))
                return false;

            var anchor = FindAnchor(service.Document, record.AnchorId);
            if(anchor?.ContentStart?.Paragraph == null)
                return false;

            var position = anchor.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            service.Selection.Select(position, position);
            service.CaretPosition = position;
            anchor.BringIntoView();
            service.Focus();
            RichTextBoxNavigation.CenterCaretInView(service.Service);
            Navigated?.Invoke(this, record.Entry.BookmarkUri!);
            return true;
        }

        public bool NavigateToUri(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            if(uri.IsAbsoluteUri || !uri.OriginalString.StartsWith('#'))
                return false;

            var anchorId = Uri.UnescapeDataString(uri.OriginalString[1..]);
            var record = index.Values.FirstOrDefault(item =>
                string.Equals(item.AnchorId, anchorId, StringComparison.Ordinal));
            return record != null && NavigateTo(source, record.Entry.Name);
        }

        public bool NavigateNext(IRichTextBoxService service) =>
            NavigateRelative(service, forward: true);

        public bool NavigatePrevious(IRichTextBoxService service) =>
            NavigateRelative(service, forward: false);

        public void InsertHyperlinkTo(
            IRichTextBoxService service,
            string bookmarkName,
            string? linkText = null)
        {
            ArgumentNullException.ThrowIfNull(service);
            if(!service.Selection.IsEmpty)
                throw new InvalidOperationException(LocalizationManager.GetString("Bookmarks.LinkSelectionNotAllowed"));
            if(string.IsNullOrWhiteSpace(bookmarkName) ||
               !index.TryGetValue(bookmarkName.Trim(), out var record))
            {
                throw new KeyNotFoundException($"Нет закладки «{bookmarkName}».");
            }

            var uri = new Uri($"#{record.AnchorId}", UriKind.Relative);
            service.BeginChange();
            try
            {
                var position = GetInsertionPosition(service);
                var link = new Hyperlink(position, position);
                link.Inlines.Add(new Run(
                    string.IsNullOrWhiteSpace(linkText) ? record.Entry.Name : linkText));

                link.NavigateUri = uri;
                MoveCaretAfterSpan(service, link);
                service.ClearSelection();
            }
            finally
            {
                service.EndChange();
            }
        }

        public void RebuildIndexFromDocument(IRichTextBoxService service)
        {
            ArgumentNullException.ThrowIfNull(service);

            ClearRecords();
            foreach(var anchor in EnumerateNamedSpans(service.Document))
            {
                var metadata = DeserializeMetadata(anchor);
                var name = metadata?.Name;
                var note = metadata?.Note ?? string.Empty;

                // Поддержка документов старого формата, где имя пользователя
                // записывалось непосредственно в Span.Name.
                if(string.IsNullOrWhiteSpace(name) &&
                   !anchor.Name.StartsWith("Bookmark_", StringComparison.Ordinal))
                {
                    name = anchor.Name;
                }

                if(string.IsNullOrWhiteSpace(name))
                    continue;

                name = MakeUniqueName(name.Trim());
                var record = CreateRecord(service, name, note, anchor.Name);
                anchor.Tag = SerializeMetadata(record.Entry);
                AddRecord(name, record);
            }

            OnPropertyChanged(nameof(Bookmarks));
        }

        private bool NavigateRelative(IRichTextBoxService service, bool forward)
        {
            ArgumentNullException.ThrowIfNull(service);
            var anchors = index.Values
                .Select(record => new
                {
                    Record = record,
                    Anchor = FindAnchor(service.Document, record.AnchorId)
                })
                .Where(item => item.Anchor?.ContentStart?.Paragraph != null)
                .OrderBy(item => item.Anchor!.ContentStart, TextPointerComparer.Instance)
                .ToList();

            if(anchors.Count == 0)
                return false;

            var caret = service.CaretPosition;
            var currentIndex = anchors.FindIndex(item =>
                item.Anchor!.ContentStart.CompareTo(caret) <= 0 &&
                item.Anchor.ContentEnd.CompareTo(caret) >= 0);

            var target = currentIndex >= 0
                ? anchors[forward
                    ? (currentIndex + 1) % anchors.Count
                    : (currentIndex - 1 + anchors.Count) % anchors.Count]
                : forward
                    ? anchors.FirstOrDefault(item => item.Anchor!.ContentStart.CompareTo(caret) > 0)
                        ?? anchors[0]
                    : anchors.LastOrDefault(item => item.Anchor!.ContentEnd.CompareTo(caret) < 0)
                        ?? anchors[^1];

            return NavigateTo(service, target.Record.Entry.Name);
        }

        private BookmarkRecord CreateRecord(
            IRichTextBoxService service,
            string name,
            string note,
            string anchorId)
        {
            var entry = new BookmarkEntryViewModel
            {
                Name = name,
                Note = note,
                BookmarkUri = new Uri($"#{anchorId}", UriKind.Relative)
            };
            var record = new BookmarkRecord(entry, anchorId);

            PropertyChangedEventHandler handler = (_, args) =>
            {
                if(args.PropertyName == nameof(IBookmarkEntryViewModel.Note))
                    UpdateAnchorMetadata(service, record);
            };
            record.Handler = handler;
            entry.PropertyChanged += handler;
            return record;
        }

        private void AddRecord(string name, BookmarkRecord record)
        {
            index[name] = record;
            Bookmarks.Add(record.Entry);
            OnPropertyChanged(nameof(Bookmarks));
        }

        private void RemoveRecord(BookmarkRecord record)
        {
            record.Entry.PropertyChanged -= record.Handler;
            index.Remove(record.Entry.Name);
            Bookmarks.Remove(record.Entry);
            OnPropertyChanged(nameof(Bookmarks));
        }

        private void ClearRecords()
        {
            foreach(var record in index.Values)
                record.Entry.PropertyChanged -= record.Handler;
            index.Clear();
            Bookmarks.Clear();
        }

        private void UpdateAnchorMetadata(IRichTextBoxService service, BookmarkRecord record)
        {
            var anchor = FindAnchor(service.Document, record.AnchorId);
            if(anchor != null)
            {
                string metadata = SerializeMetadata(record.Entry);
                if(Equals(anchor.Tag, metadata))
                    return;
                anchor.Tag = metadata;
                MetadataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void EnsureValidNewName(string name)
        {
            if(name.Length > 128 || name.Any(char.IsControl) || name.Contains('#'))
                throw new ArgumentException("Недопустимое имя закладки.", nameof(name));
            if(index.ContainsKey(name))
                throw new InvalidOperationException($"Закладка «{name}» уже существует.");
        }

        private static string NormalizeName(string? name)
        {
            var result = name?.Trim() ?? string.Empty;
            if(result.Length == 0)
                throw new ArgumentException("Имя закладки не задано.", nameof(name));
            return result;
        }

        private string MakeUniqueName(string name)
        {
            if(!index.ContainsKey(name))
                return name;

            var number = 2;
            while(index.ContainsKey($"{name} ({number})"))
                number++;
            return $"{name} ({number})";
        }

        private static TextPointer GetInsertionPosition(IRichTextBoxService service)
        {
            var position = service.CaretPosition;
            return position.IsAtInsertionPosition
                ? position
                : position.GetInsertionPosition(LogicalDirection.Forward);
        }

        private static Span? FindAnchor(FlowDocument document, string anchorId) =>
            EnumerateNamedSpans(document)
                .FirstOrDefault(span => string.Equals(
                    span.Name,
                    anchorId,
                    StringComparison.Ordinal));

        private static void MoveCaretAfterSpan(IRichTextBoxService service, Span span)
        {
            // Отдельный Run и направление Forward не дают WPF включить
            // последующий обычный ввод в удаляемый текст закладки или ссылки.
            var followingText = new Run();
            span.SiblingInlines!.InsertAfter(span, followingText);
            service.CaretPosition = followingText.ContentStart.GetPositionAtOffset(0, LogicalDirection.Forward);
        }

        private static bool ContainsSpan(Span parent, Span child) =>
            parent == child ||
            (parent.ContentStart.CompareTo(child.ElementStart) <= 0 &&
             parent.ContentEnd.CompareTo(child.ElementEnd) >= 0);

        private static IEnumerable<Span> EnumerateNamedSpans(FlowDocument document)
        {
            var seen = new HashSet<Span>();
            for(var position = document.ContentStart;
                position != null && position.CompareTo(document.ContentEnd) < 0;
                position = position.GetNextContextPosition(LogicalDirection.Forward))
            {
                if(position.GetAdjacentElement(LogicalDirection.Forward) is Span span &&
                   !string.IsNullOrWhiteSpace(span.Name) &&
                   seen.Add(span))
                {
                    yield return span;
                }
            }
        }

        private static string SerializeMetadata(IBookmarkEntryViewModel entry) =>
            MetadataPrefix + JsonSerializer.Serialize(
                new BookmarkMetadata(entry.Name, entry.Note ?? string.Empty));

        private static BookmarkMetadata? DeserializeMetadata(Span anchor)
        {
            if(anchor.Tag is not string value ||
               !value.StartsWith(MetadataPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<BookmarkMetadata>(
                    value[MetadataPrefix.Length..]);
            }
            catch(JsonException)
            {
                return null;
            }
        }

        private sealed class BookmarkRecord
        {
            public BookmarkEntryViewModel Entry { get; }
            public string AnchorId { get; }
            public PropertyChangedEventHandler Handler { get; set; } = null!;

            public BookmarkRecord(BookmarkEntryViewModel entry, string anchorId)
            {
                Entry = entry;
                AnchorId = anchorId;
            }
        }

        private sealed record BookmarkMetadata(string Name, string Note);

        private sealed class TextPointerComparer: IComparer<TextPointer>
        {
            public static TextPointerComparer Instance { get; } = new();
            public int Compare(TextPointer? x, TextPointer? y) =>
                x == null ? (y == null ? 0 : -1)
                : y == null ? 1
                : x.CompareTo(y);
        }
    }
}

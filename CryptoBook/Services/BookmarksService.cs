using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public class BookmarksService:ViewModelBase, IBookmarkService
    {
        private readonly IRichTextBoxService service;
        private readonly Dictionary<string, BookmarkEntryViewModel> index;

        private bool IsValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');

        public ObservableCollection<BookmarkEntryViewModel> Bookmarks { get; } = [];
        public bool Exists(string name)=> index.ContainsKey(name);


        public BookmarksService(IRichTextBoxService service)
        {
            this.service = service;
            index= new(StringComparer.Ordinal);
            //testAddIndex();
        }

        private void testAddIndex()
        {
            var vm = new BookmarkEntryViewModel { Name = "Test" };
            Bookmarks.Add(vm);
            index[vm.Name] = vm;
            vm=new BookmarkEntryViewModel { Name = "Test2" };
            Bookmarks.Add(vm);
            index[vm.Name] = vm;
        }


        public void AddAtCaret(IRichTextBoxService service, string name)
        {
            if(service is null)
                throw new ArgumentNullException(nameof(service));
            if(!IsValidName(name))
                throw new ArgumentException("Недопустимое имя закладки.", nameof(name));
            if(index.ContainsKey(name))
                throw new InvalidOperationException($"Закладка «{name}» уже существует.");

            // 1) вставляем якорь в документ
            const string ZWSP = "\u200B";
            var pos = GetInsertionPos(service);
            service.BeginChange();
            try
            {
                var tr = new TextRange(pos, pos) { Text = ZWSP };
                var start = tr.Start;
                var end = start.GetPositionAtOffset(1, LogicalDirection.Forward)!;
                var span = new Span(start, end) { Name = name };
                service.CaretPosition = span.ElementEnd;
            } finally { service.EndChange(); }

            // 2) регистрируем в модели
            var vm = new BookmarkEntryViewModel
            {
                Name = name
            };
            Bookmarks.Add(vm);
            index[name] = vm;
        }

        public bool Remove(IRichTextBoxService service, string name)
        {
            if(!index.TryGetValue(name, out var vm))
                return false;

            // убрать из документа
            if(service?.Document != null && service.Document.FindName(name) is Span span)
            {
                service.BeginChange();
                try
                { new TextRange(span.ContentStart, span.ContentEnd).Text = string.Empty; } finally { service.EndChange(); }
            }

            // убрать из коллекции + индекса
            index.Remove(name);
            Bookmarks.Remove(vm);
            return true;
        }

        public void Rename(IRichTextBoxService service, string oldName, string newName)
        {
            if(!index.TryGetValue(oldName, out var vm))
                throw new KeyNotFoundException($"Нет закладки «{oldName}».");
            if(!IsValidName(newName))
                throw new ArgumentException("Недопустимое имя закладки.", nameof(newName));
            if(index.ContainsKey(newName))
                throw new InvalidOperationException($"Закладка «{newName}» уже существует.");

            // документ
            if(service?.Document != null && service.Document.FindName(oldName) is Span span)
                span.Name = newName;

            // индекс и ВМ
            index.Remove(oldName);
            vm.Name = newName;           // триггерит Uri и INotifyPropertyChanged
            index[newName] = vm;
        }

        public bool NavigateTo(IRichTextBoxService service, string name)
        {
            if(service?.Document is null)
                return false;
            var el = service.Document.FindName(name) as TextElement;
            if(el is null)
                return false;

            service.Selection.Select(el.ContentStart, el.ContentStart);
            service.CaretPosition = el.ContentStart;
            el.BringIntoView();
            service.Focus();
            return true;
        }

        public void InsertHyperlinkTo(IRichTextBoxService service, string bookmarkName, string? linkText = null)
        {
            if(!index.TryGetValue(bookmarkName, out var vm))
                throw new KeyNotFoundException($"Нет закладки «{bookmarkName}».");

            var run = new Run(linkText ?? bookmarkName, service.CaretPosition);
            var link = new Hyperlink(run) { NavigateUri = vm.BookmarkUri };

            link.RequestNavigate += (s, e) =>
            {
                if(e.Uri.IsAbsoluteUri)
                {
                    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                } else
                {
                    var target = e.Uri.OriginalString.TrimStart('#');
                    NavigateTo(service, target);
                }
                e.Handled = true;
            };
        }



        public void RebuildIndexFromDocument(IRichTextBoxService service)
        {
            if(service?.Document is null)
                return;

            Bookmarks.Clear();
            index.Clear();

            // простой проход по документу
            for(var p = service.Document.ContentStart;
                 p != null && p.CompareTo(service.Document.ContentEnd) < 0;
                 p = p.GetNextContextPosition(LogicalDirection.Forward))
            {
                if(p.GetAdjacentElement(LogicalDirection.Forward) is Span s && !string.IsNullOrEmpty(s.Name))
                {
                    var vm = new BookmarkEntryViewModel() { Name=s.Name};
                    Bookmarks.Add(vm);
                    index[vm.Name] = vm;
                }
            }
        }



        private TextPointer GetInsertionPos(IRichTextBoxService svc)
        {
            var pos = svc.CaretPosition;
            return pos.IsAtInsertionPosition ? pos : pos.GetInsertionPosition(LogicalDirection.Forward);
        }

    }
}

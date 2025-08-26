using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public class BookmarkValidationService:IBookmarkValidationService
    {
        private readonly IBookmarkService bookmarkService;
        private readonly Regex NameRx = new(@"^[\w\-.]+$", RegexOptions.Compiled);


        public BookmarkValidationService(IBookmarkService bookmarkService)
        {
            this.bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
        }



        public ValidationResult CanInsertBookmark(IRichTextBoxService svc, string name, Func<string, bool> existsByName)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument, "Документ отсутствует.");
            if(svc.IsReadOnly)
                return ValidationResult.Fail(ValidationCode.ReadOnly, "Документ только для чтения.");
            if(string.IsNullOrWhiteSpace(name))
                return ValidationResult.Fail(ValidationCode.NameEmpty, "Имя закладки пустое.");
            if(!NameRx.IsMatch(name))
                return ValidationResult.Fail(ValidationCode.NameInvalid, "Недопустимые символы.");
            if(bookmarkService.Exists(name))
                return ValidationResult.Fail(ValidationCode.NameExists, $"«{name}» уже существует.");

            var pos = svc.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
            if(pos is null)
                return ValidationResult.Fail(ValidationCode.BadCaret, "Недоступная позиция каретки.");
            if(GetAncestor<Hyperlink>(pos) != null)
                return ValidationResult.Fail(ValidationCode.InsideHyperlink, "Внутри ссылки.");

            return ValidationResult.Success();
        }

        public ValidationResult CanRenameBookmark(IRichTextBoxService svc, string oldName, string newName, Func<string, bool> existsByName)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument);
            if(svc.IsReadOnly)
                return ValidationResult.Fail(ValidationCode.ReadOnly);
            if(string.IsNullOrWhiteSpace(oldName))
                return ValidationResult.Fail(ValidationCode.NameEmpty, "Старое имя пустое.");
            if(string.IsNullOrWhiteSpace(newName))
                return ValidationResult.Fail(ValidationCode.NameEmpty, "Новое имя пустое.");
            if(!NameRx.IsMatch(newName))
                return ValidationResult.Fail(ValidationCode.NameInvalid);
            if(string.Equals(oldName, newName, StringComparison.Ordinal))
                return ValidationResult.Fail(ValidationCode.NameInvalid, "Имя не изменилось.");
            if(svc.Document.FindName(oldName) is not TextElement)
                return ValidationResult.Fail(ValidationCode.NameNotFound, $"«{oldName}» не найдена.");
            if(bookmarkService.Exists(newName))
                return ValidationResult.Fail(ValidationCode.NameExists, $"«{newName}» уже существует.");
            return ValidationResult.Success();
        }

        public ValidationResult CanRemoveBookmark(IRichTextBoxService svc, string name)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument);
            if(svc.IsReadOnly)
                return ValidationResult.Fail(ValidationCode.ReadOnly);
            if(string.IsNullOrWhiteSpace(name))
                return ValidationResult.Fail(ValidationCode.NameEmpty);
            if(svc.Document.FindName(name) is not TextElement)
                return ValidationResult.Fail(ValidationCode.NameNotFound, $"«{name}» не найдена.");
            return ValidationResult.Success();
        }

        public ValidationResult CanInsertHyperlink(IRichTextBoxService svc, string? linkText)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument);
            if(svc.IsReadOnly)
                return ValidationResult.Fail(ValidationCode.ReadOnly);

            var sel = svc.Selection;
            var hasSelection = !sel.IsEmpty;

            var caret = svc.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
            if(caret is null)
                return ValidationResult.Fail(ValidationCode.BadCaret);
            if(GetAncestor<Hyperlink>(caret) != null)
                return ValidationResult.Fail(ValidationCode.InsideHyperlink);

            if(hasSelection)
            {
                var start = sel.Start.GetInsertionPosition(LogicalDirection.Forward);
                var end = sel.End.GetInsertionPosition(LogicalDirection.Backward);
                if(start is null || end is null || start.CompareTo(end) >= 0)
                    return ValidationResult.Fail(ValidationCode.BadSelection);
                if(!IsInlineRange(start, end))
                    return ValidationResult.Fail(ValidationCode.RangeNotInline, "Только inline в пределах одного абзаца.");
                if(HasHyperlinkInRange(start, end))
                    return ValidationResult.Fail(ValidationCode.OverlapsHyperlink, "Пересечение с другой ссылкой.");
            } else
            {
                if(string.IsNullOrWhiteSpace(linkText))
                    return ValidationResult.Fail(ValidationCode.NeedLinkText, "Укажите текст ссылки или выделите текст.");
            }

            return ValidationResult.Success();
        }

        public ValidationResult CanNavigateTo(IRichTextBoxService svc, string name)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument, "Документ отсутствует.");

            if(string.IsNullOrWhiteSpace(name))
                return ValidationResult.Fail(ValidationCode.NameEmpty, "Имя пустое.");

            if(svc.Document.FindName(name) is not TextElement el)
                return ValidationResult.Fail(ValidationCode.NameNotFound, $"Закладка «{name}» не найдена.");

            // Базовая проверка «живости» позиции: элемент в потоке текста и имеет Paragraph-предка
            if(el.ContentStart?.Paragraph is null)
                return ValidationResult.Fail(ValidationCode.TargetDetached, "Цель не привязана к текстовому потоку.");

            return ValidationResult.Success();
        }


        public ValidationResult CanRebuildIndexFromDocument(IRichTextBoxService svc)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument, "Документ отсутствует.");

            // Пересборка только читает документ и обновляет вашу коллекцию — режим ReadOnly не критичен.
            return ValidationResult.Success();
        }



        // ---- helpers ----
        static T? GetAncestor<T>(TextPointer pos) where T : TextElement
        {
            for(TextElement? el = pos.Parent as TextElement; el != null; el = el.Parent as TextElement)
                if(el is T t)
                    return t;
            return null;
        }

        static bool IsInlineRange(TextPointer start, TextPointer end)
        {
            var p1 = start.Paragraph;
            var p2 = end.Paragraph;
            if(p1 == null || p2 == null || !ReferenceEquals(p1, p2))
                return false;
            return IsInlineSide(start) && IsInlineSide(end);
        }

        static bool IsInlineSide(TextPointer p)
        {
            for(TextElement? el = p.Parent as TextElement; el != null; el = el.Parent as TextElement)
            {
                if(el is Paragraph)
                    return true;
                if(el is Inline)
                    continue;
                return false;
            }
            return false;
        }

        static bool HasHyperlinkInRange(TextPointer start, TextPointer end)
        {
            for(var nav = start; nav != null && nav.CompareTo(end) < 0; nav = nav.GetNextContextPosition(LogicalDirection.Forward))
                if(nav.GetAdjacentElement(LogicalDirection.Forward) is Hyperlink)
                    return true;
            return false;
        }
    }
}

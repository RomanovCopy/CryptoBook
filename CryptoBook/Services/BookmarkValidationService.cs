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
            throw new NotImplementedException();
        }

        public ValidationResult CanRemoveBookmark(IRichTextBoxService svc, string name)
        {
            throw new NotImplementedException();
        }

        public ValidationResult CanInsertHyperlink(IRichTextBoxService svc, string? linkText)
        {
            throw new NotImplementedException();
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

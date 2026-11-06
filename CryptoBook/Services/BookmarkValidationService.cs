using CryptoBook.Interfaces;

using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public class BookmarkValidationService:IBookmarkValidationService
    {
        private readonly IBookmarkService bookmarkService;
        public BookmarkValidationService(IBookmarkService bookmarkService)
        {
            this.bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
        }



        public ValidationResult CanInsertBookmark(IRichTextBoxService svc, string name, Func<string, bool> existsByName)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument,
                    LocalizationManager.GetString("Bookmarks.NoDocument"));
            if(svc.IsReadOnly)
                return ValidationResult.Fail(ValidationCode.ReadOnly,
                    LocalizationManager.GetString("Bookmarks.ReadOnly"));
            if(!svc.Selection.IsEmpty)
                return ValidationResult.Fail(ValidationCode.BadSelection,
                    LocalizationManager.GetString("Bookmarks.SelectionNotAllowed"));
            if(string.IsNullOrWhiteSpace(name))
                return ValidationResult.Fail(ValidationCode.NameEmpty,
                    LocalizationManager.GetString("Bookmarks.NameEmpty"));
            name = name.Trim();
            if(name.Length > 128 || name.Any(char.IsControl) || name.Contains('#'))
                return ValidationResult.Fail(ValidationCode.NameInvalid,
                    LocalizationManager.GetString("Bookmarks.NameInvalid"));
            if(existsByName(name))
                return ValidationResult.Fail(ValidationCode.NameExists,
                    LocalizationManager.Format("Bookmarks.NameExists", name));

            var pos = svc.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
            if(pos is null)
                return ValidationResult.Fail(ValidationCode.BadCaret,
                    LocalizationManager.GetString("Bookmarks.BadCaret"));
            if(GetAncestor<Hyperlink>(pos) != null)
                return ValidationResult.Fail(ValidationCode.InsideHyperlink,
                    LocalizationManager.GetString("Bookmarks.InsideHyperlink"));

            return ValidationResult.Success();
        }

        public ValidationResult CanRenameBookmark(IRichTextBoxService svc, string oldName, string newName, Func<string, bool> existsByName)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument,
                    LocalizationManager.GetString("Bookmarks.NoDocument"));
            if(svc.IsReadOnly)
                return ValidationResult.Fail(ValidationCode.ReadOnly,
                    LocalizationManager.GetString("Bookmarks.ReadOnly"));
            if(string.IsNullOrWhiteSpace(oldName))
                return ValidationResult.Fail(ValidationCode.NameEmpty,
                    LocalizationManager.GetString("Bookmarks.OldNameEmpty"));
            if(string.IsNullOrWhiteSpace(newName))
                return ValidationResult.Fail(ValidationCode.NameEmpty,
                    LocalizationManager.GetString("Bookmarks.NewNameEmpty"));
            oldName = oldName.Trim();
            newName = newName.Trim();
            if(newName.Length > 128 || newName.Any(char.IsControl) || newName.Contains('#'))
                return ValidationResult.Fail(ValidationCode.NameInvalid,
                    LocalizationManager.GetString("Bookmarks.NameInvalid"));
            if(string.Equals(oldName, newName, StringComparison.Ordinal))
                return ValidationResult.Fail(ValidationCode.NameUnchanged,
                    LocalizationManager.GetString("Bookmarks.NameUnchanged"));
            if(!existsByName(oldName))
                return ValidationResult.Fail(ValidationCode.NameNotFound,
                    LocalizationManager.Format("Bookmarks.NameNotFound", oldName));
            if(existsByName(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Fail(ValidationCode.NameExists,
                    LocalizationManager.Format("Bookmarks.NameExists", newName));
            return ValidationResult.Success();
        }

        public ValidationResult CanRemoveBookmark(IRichTextBoxService svc, string name)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument,
                    LocalizationManager.GetString("Bookmarks.NoDocument"));
            if(svc.IsReadOnly)
                return ValidationResult.Fail(ValidationCode.ReadOnly,
                    LocalizationManager.GetString("Bookmarks.ReadOnly"));
            if(string.IsNullOrWhiteSpace(name))
                return ValidationResult.Fail(ValidationCode.NameEmpty,
                    LocalizationManager.GetString("Bookmarks.NameEmpty"));
            if(!bookmarkService.Exists(name))
                return ValidationResult.Fail(ValidationCode.NameNotFound,
                    LocalizationManager.Format("Bookmarks.NameNotFound", name));
            return ValidationResult.Success();
        }

        public ValidationResult CanInsertHyperlink(IRichTextBoxService svc, string? linkText)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument,
                    LocalizationManager.GetString("Bookmarks.NoDocument"));
            if(svc.IsReadOnly)
                return ValidationResult.Fail(ValidationCode.ReadOnly,
                    LocalizationManager.GetString("Bookmarks.ReadOnly"));

            if(!svc.Selection.IsEmpty)
                return ValidationResult.Fail(ValidationCode.BadSelection,
                    LocalizationManager.GetString("Bookmarks.LinkSelectionNotAllowed"));
            if(string.IsNullOrWhiteSpace(linkText))
                return ValidationResult.Fail(ValidationCode.NeedLinkText,
                    LocalizationManager.GetString("Bookmarks.NeedLinkText"));

            var caret = svc.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
            if(caret is null)
                return ValidationResult.Fail(ValidationCode.BadCaret,
                    LocalizationManager.GetString("Bookmarks.BadCaret"));
            if(GetAncestor<Hyperlink>(caret) != null)
                return ValidationResult.Fail(ValidationCode.InsideHyperlink,
                    LocalizationManager.GetString("Bookmarks.InsideHyperlink"));

            return ValidationResult.Success();
        }

        public ValidationResult CanNavigateTo(IRichTextBoxService svc, string name)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument,
                    LocalizationManager.GetString("Bookmarks.NoDocument"));

            if(string.IsNullOrWhiteSpace(name))
                return ValidationResult.Fail(ValidationCode.NameEmpty,
                    LocalizationManager.GetString("Bookmarks.NameEmpty"));

            if(!bookmarkService.Exists(name))
                return ValidationResult.Fail(ValidationCode.NameNotFound,
                    LocalizationManager.Format("Bookmarks.NameNotFound", name));

            return ValidationResult.Success();
        }


        public ValidationResult CanRebuildIndexFromDocument(IRichTextBoxService svc)
        {
            if(svc?.Document is null)
                return ValidationResult.Fail(ValidationCode.NoDocument,
                    LocalizationManager.GetString("Bookmarks.NoDocument"));

            // Пересборка только читает документ и обновляет вашу коллекцию — режим ReadOnly не критичен.
            return ValidationResult.Success();
        }



        // ---- Вспомогательные методы ----
        static T? GetAncestor<T>(TextPointer pos) where T : TextElement
        {
            for(TextElement? el = pos.Parent as TextElement; el != null; el = el.Parent as TextElement)
                if(el is T t)
                    return t;
            return null;
        }

    }
}

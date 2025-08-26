using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IBookmarkValidationService
    {
        ValidationResult CanInsertBookmark(IRichTextBoxService svc, string name, Func<string, bool> existsByName);
        ValidationResult CanRenameBookmark(IRichTextBoxService svc, string oldName, string newName, Func<string, bool> existsByName);
        ValidationResult CanRemoveBookmark(IRichTextBoxService svc, string name);
        ValidationResult CanInsertHyperlink(IRichTextBoxService svc, string? linkText);
        ValidationResult CanNavigateTo(IRichTextBoxService svc, string name);
        ValidationResult CanRebuildIndexFromDocument(IRichTextBoxService svc);
    }

    public enum ValidationCode
    {
        Ok = 0,

        // общие
        NoDocument,
        ReadOnly,
        BadCaret,
        BadSelection,

        // имена
        NameEmpty,
        NameInvalid,
        NameExists,
        NameNotFound,
        NameUnchanged,

        // ссылки/диапазоны
        RangeNotInline,
        InsideHyperlink,
        OverlapsHyperlink,
        NeedLinkText,

        // навигация
        TargetDetached // цель найдена, но не привязана к визуальному/поточному дереву
    }

    public readonly struct ValidationResult
    {
        public bool Ok => Code == ValidationCode.Ok;
        public ValidationCode Code { get; }
        public string? Message { get; }

        public static ValidationResult Success()
            => new (ValidationCode.Ok, null);

        public static ValidationResult Fail(ValidationCode code, string? msg = null)
            => new (code, msg);

        private ValidationResult(ValidationCode code, string? message)
        {
            Code = code;
            Message = message;
        }

        public override string ToString() => Ok ? "Ok" : $"{Code}: {Message}";
    }

}

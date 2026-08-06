using CryptoBook.Interfaces;

using System.Globalization;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public sealed class FlowDocumentContentInspector:
        IDocumentContentInspector
    {
        public bool HasPrintableContent(FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            for(TextPointer? position = document.ContentStart;
                position is not null &&
                position.CompareTo(document.ContentEnd) < 0;
                position = position.GetNextContextPosition(
                    LogicalDirection.Forward))
            {
                TextPointerContext context = position.GetPointerContext(
                    LogicalDirection.Forward);
                if(context == TextPointerContext.Text &&
                   ContainsVisibleText(position.GetTextInRun(
                       LogicalDirection.Forward)))
                {
                    return true;
                }

                if(context == TextPointerContext.EmbeddedElement)
                    return true;

                if(context == TextPointerContext.ElementStart &&
                   position.GetAdjacentElement(LogicalDirection.Forward) is
                       List or Table)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsVisibleText(string text) =>
            text.Any(character =>
            {
                UnicodeCategory category = char.GetUnicodeCategory(character);
                return !char.IsWhiteSpace(character) &&
                    category is not UnicodeCategory.Control and
                    not UnicodeCategory.Format;
            });
    }
}

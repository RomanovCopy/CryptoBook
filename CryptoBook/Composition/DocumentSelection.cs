using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Composition
{
    public class DocumentSelection:IDocumentSelection
    {
        private readonly IRichTextBoxService _rtb;
        public DocumentSelection(IRichTextBoxService rtb) => _rtb = rtb ?? throw new ArgumentNullException(nameof(rtb));

        public TextSelection Selection => _rtb.Selection;

        public IReadOnlyList<Paragraph> GetSelectedParagraphsOrCurrent()
        {
            if(Selection == null || Selection.IsEmpty)
                return Array.Empty<Paragraph>();

            var startPara = Selection.Start?.Paragraph ?? GetNextParagraph(Selection.Start);
            var endPara = Selection.End?.Paragraph ?? GetPreviousParagraph(Selection.End);

            if(startPara == null || endPara == null)
                return Array.Empty<Paragraph>();

            return EnumerateParagraphs(startPara, endPara).ToList();
        }

        // --- helpers (локальные) ---

        private static IEnumerable<Paragraph> EnumerateParagraphs(Paragraph start, Paragraph end)
        {
            for(var p = start; p != null; p = GetNextParagraphAfter(p))
            {
                yield return p;
                if(ReferenceEquals(p, end))
                    break;
            }
        }

        private static Paragraph? GetNextParagraph(TextPointer pos)
        {
            var p = pos?.Paragraph;
            if(p != null)
                return p;
            var next = pos?.GetNextContextPosition(LogicalDirection.Forward);
            while(next != null && next.Paragraph == null)
                next = next.GetNextContextPosition(LogicalDirection.Forward);
            return next?.Paragraph;
        }

        private static Paragraph? GetPreviousParagraph(TextPointer pos)
        {
            var prev = pos?.GetNextContextPosition(LogicalDirection.Backward);
            while(prev != null && prev.Paragraph == null)
                prev = prev.GetNextContextPosition(LogicalDirection.Backward);
            return prev?.Paragraph;
        }

        private static Paragraph? GetNextParagraphAfter(Paragraph p)
        {
            var next = p.ElementEnd?.GetNextContextPosition(LogicalDirection.Forward);
            while(next != null && next.Paragraph == null)
                next = next.GetNextContextPosition(LogicalDirection.Forward);
            return next?.Paragraph;
        }
    }
}

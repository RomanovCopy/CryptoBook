using System.Text;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    internal readonly record struct TextSearchMatch(int Start, int Length);

    internal static class DocumentTextSearch
    {
        public static IReadOnlyList<TextSearchMatch> Find(
            string text, string query, bool matchCase)
        {
            if(string.IsNullOrEmpty(query))
                return [];

            var matches = new List<TextSearchMatch>();
            var comparison = matchCase ? StringComparison.Ordinal :
                StringComparison.OrdinalIgnoreCase;
            int offset = 0;
            while(offset <= text.Length - query.Length)
            {
                int index = text.IndexOf(query, offset, comparison);
                if(index < 0)
                    break;
                matches.Add(new TextSearchMatch(index, query.Length));
                offset = index + query.Length;
            }
            return matches;
        }

        public static IReadOnlyList<TextRange> Find(
            FlowDocument document, string query, bool matchCase)
        {
            if(string.IsNullOrEmpty(query))
                return [];

            // Inline boundaries are formatting, not text boundaries. Keep a map
            // of text runs so matches can span bold, links and other inlines.
            var text = new StringBuilder();
            var runs = new List<(int Offset, int Length, TextPointer Position)>();
            TextPointer? position = document.ContentStart;
            while(position is not null && position.CompareTo(document.ContentEnd) < 0)
            {
                var context = position.GetPointerContext(LogicalDirection.Forward);
                if(context == TextPointerContext.Text)
                {
                    string run = position.GetTextInRun(LogicalDirection.Forward);
                    runs.Add((text.Length, run.Length, position));
                    text.Append(run);
                    position = position.GetPositionAtOffset(run.Length);
                    continue;
                }

                if(context == TextPointerContext.EmbeddedElement)
                    text.Append('\uFFFC');
                else if(context == TextPointerContext.ElementStart &&
                        position.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)
                    text.Append('\n');
                else if(context == TextPointerContext.ElementEnd)
                {
                    var element = position.GetAdjacentElement(LogicalDirection.Forward);
                    if(element is Paragraph or TableRow)
                        text.Append('\n');
                    else if(element is TableCell)
                        text.Append('\t');
                }

                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }

            var results = new List<TextRange>();
            int startRun = 0;
            foreach(var match in Find(text.ToString(), query.Replace("\r\n", "\n"), matchCase))
            {
                while(startRun < runs.Count &&
                      runs[startRun].Offset + runs[startRun].Length <= match.Start)
                    startRun++;
                int endRun = startRun;
                int last = match.Start + match.Length - 1;
                while(endRun < runs.Count && runs[endRun].Offset + runs[endRun].Length <= last)
                    endRun++;
                if(startRun >= runs.Count || endRun >= runs.Count ||
                   runs[startRun].Offset > match.Start || runs[endRun].Offset > last)
                    continue;

                var start = runs[startRun].Position.GetPositionAtOffset(
                    match.Start - runs[startRun].Offset)!;
                var end = runs[endRun].Position.GetPositionAtOffset(
                    last - runs[endRun].Offset + 1)!;
                results.Add(new TextRange(start, end));
            }
            return results;
        }
    }
}

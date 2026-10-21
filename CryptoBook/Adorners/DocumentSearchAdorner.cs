using CryptoBook.Services;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using TextBox = System.Windows.Controls.TextBox;
using RichTextBox = System.Windows.Controls.RichTextBox;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;

namespace CryptoBook.Adorners
{
    internal sealed class DocumentSearchAdorner: Adorner
    {
        private static readonly SolidColorBrush MatchBrush = CreateBrush(100, 255, 213, 0);
        private static readonly SolidColorBrush CurrentBrush = CreateBrush(135, 255, 145, 0);
        private readonly TextBoxBase editor;
        private readonly IReadOnlyList<TextRange> richMatches;
        private readonly IReadOnlyList<TextSearchMatch> textMatches;

        public DocumentSearchAdorner(TextBoxBase editor,
            IReadOnlyList<TextRange> richMatches, IReadOnlyList<TextSearchMatch> textMatches): base(editor)
        {
            this.editor = editor;
            this.richMatches = richMatches;
            this.textMatches = textMatches;
            IsHitTestVisible = false;
        }

        public int CurrentIndex { get; set; }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var viewport = new Rect(editor.RenderSize);
            drawingContext.PushClip(new RectangleGeometry(viewport));
            foreach(var (index, rectangle) in GetVisibleRectangles())
                drawingContext.DrawRectangle(index == CurrentIndex ? CurrentBrush : MatchBrush,
                    null, rectangle);
            drawingContext.Pop();
        }

        // Only visible characters need geometry. No document properties are
        // changed, so searching cannot dirty a file or enter the undo history.
        internal IEnumerable<(int Index, Rect Rectangle)> GetVisibleRectangles()
        {
            var viewport = new Rect(editor.RenderSize);
            if(editor is RichTextBox rich)
            {
                var visibleStart = rich.GetPositionFromPoint(new Point(0, 0), true);
                var visibleEnd = rich.GetPositionFromPoint(new Point(rich.ActualWidth, rich.ActualHeight), true);
                for(int index = 0; index < richMatches.Count; index++)
                {
                    var range = richMatches[index];
                    if(visibleStart is not null && range.End.CompareTo(visibleStart) < 0)
                        continue;
                    if(visibleEnd is not null && range.Start.CompareTo(visibleEnd) > 0)
                        break;
                    TextPointer? position = range.Start;
                    if(visibleStart is not null && position.CompareTo(visibleStart) < 0)
                        position = visibleStart;
                    while(position is not null && position.CompareTo(range.End) < 0 && position.HasValidLayout)
                    {
                        var next = position.GetNextInsertionPosition(LogicalDirection.Forward);
                        if(next is null)
                            break;
                        if(visibleEnd is not null && position.CompareTo(visibleEnd) > 0)
                            break;
                        var rectangle = Between(position.GetCharacterRect(LogicalDirection.Forward),
                            next.GetCharacterRect(LogicalDirection.Backward));
                        if(!rectangle.IsEmpty && rectangle.IntersectsWith(viewport))
                            yield return (index, rectangle);
                        position = next;
                    }
                }
            }
            else if(editor is TextBox text)
            {
                int first = text.GetCharacterIndexFromPoint(new Point(0, 0), true);
                int last = text.GetCharacterIndexFromPoint(new Point(text.ActualWidth, text.ActualHeight), true);
                for(int index = 0; index < textMatches.Count; index++)
                {
                    var match = textMatches[index];
                    int end = Math.Min(match.Start + match.Length, last + 1);
                    for(int character = Math.Max(match.Start, first); character < end; character++)
                    {
                        var rectangle = Between(text.GetRectFromCharacterIndex(character),
                            text.GetRectFromCharacterIndex(character, true));
                        if(!rectangle.IsEmpty && rectangle.IntersectsWith(viewport))
                            yield return (index, rectangle);
                    }
                }
            }
        }

        private static Rect Between(Rect start, Rect end)
        {
            if(start.IsEmpty || end.IsEmpty || Math.Abs(start.Top - end.Top) > 1)
                return Rect.Empty;
            return new Rect(Math.Min(start.Left, end.Left), start.Top,
                Math.Max(1, Math.Abs(end.Left - start.Left)), Math.Max(start.Height, end.Height));
        }

        private static SolidColorBrush CreateBrush(byte alpha, byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}

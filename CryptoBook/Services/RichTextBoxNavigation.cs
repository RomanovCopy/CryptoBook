using CryptoBook.Adorners;
using System.Windows.Documents;
using System.Windows.Threading;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace CryptoBook.Services;

internal static class RichTextBoxNavigation
{
    public static void CenterCaretInView(RichTextBox editor)
    {
        if(!editor.IsLoaded)
            return;
        BookmarkNavigationAdorner.Clear(editor);
        var document = editor.Document;
        var position = editor.CaretPosition;

        // Caret changes and BringIntoView also schedule WPF scrolling. Apply the
        // final position after those requests and layout, including at non-100% zoom.
        editor.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if(!editor.IsLoaded || !ReferenceEquals(editor.Document, document) ||
               editor.CaretPosition.CompareTo(position) != 0)
                return;
            editor.UpdateLayout();
            if(!position.HasValidLayout || editor.ViewportHeight <= 0)
                return;
            var bounds = position.GetCharacterRect(LogicalDirection.Forward);
            if(bounds.IsEmpty || !double.IsFinite(bounds.Top) || !double.IsFinite(bounds.Height))
                return;
            double offset = editor.VerticalOffset + bounds.Top + bounds.Height / 2 - editor.ViewportHeight / 2;
            editor.ScrollToVerticalOffset(Math.Clamp(offset, 0,
                Math.Max(0, editor.ExtentHeight - editor.ViewportHeight)));
            BookmarkNavigationAdorner.Show(editor, position);
        }));
    }
}

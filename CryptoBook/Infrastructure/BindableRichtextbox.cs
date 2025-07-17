using CryptoBook.Interfaces;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;
namespace CryptoBook.Infrastructure
{
    public class BindableRichtextbox: System.Windows.Controls.RichTextBox, IRichTextBoxService
    {
        public static readonly DependencyProperty DocumentContentProperty =
            DependencyProperty.Register(
                nameof(DocumentContent),
                typeof(FlowDocument),
                typeof(BindableRichtextbox),
                new PropertyMetadata(null, OnDocumentContentChanged));

        public FlowDocument DocumentContent
        {
            get => (FlowDocument)GetValue(DocumentContentProperty);
            set => SetValue(DocumentContentProperty, value);
        }

        private static void OnDocumentContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var richTextBox = (BindableRichtextbox)d;
            richTextBox.Document = e.NewValue as FlowDocument ?? new FlowDocument();
        }


        TextSelection IRichTextBoxService.SelectedText => Selection;

        bool IRichTextBoxService.CanUndo => base.CanUndo;

        bool IRichTextBoxService.CanRedo => base.CanRedo;

        TextPointer IRichTextBoxService.CaretPosition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        bool IRichTextBoxService.IsReadOnly { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        bool IRichTextBoxService.SpellCheckEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        void IRichTextBoxService.ApplyBold()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyItalic()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyUnderline()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyFontSize(double fontSize)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyFontFamily(string fontFamily)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyForegroundColor(Color color)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyBackgroundColor(Color color)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyTextAlignment(TextAlignment alignment)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ClearFormatting()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.SelectAll()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ClearSelection()
        {
            throw new NotImplementedException();
        }

        string IRichTextBoxService.GetSelectedTextAsString()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ReplaceSelectedText(string text)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.InsertHyperlink(string uri, string displayText)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.InsertImage(string imagePath, double width, double height)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.InsertParagraph()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.InsertLineBreak()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.InsertTable(int rows, int columns)
        {
            throw new NotImplementedException();
        }

        string IRichTextBoxService.GetRtf()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.LoadRtf(string rtf)
        {
            throw new NotImplementedException();
        }

        string IRichTextBoxService.GetPlainText()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.LoadPlainText(string text)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ClearDocument()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ScrollToCaret()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ScrollToEnd()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ScrollToStart()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.SetDocumentMargin(Thickness margin)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.Undo()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.Redo()
        {
            throw new NotImplementedException();
        }

        bool IRichTextBoxService.FindText(string searchText, bool matchCase, bool wholeWord)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ReplaceText(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ReplaceAllText(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyBulletedList()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.ApplyNumberedList()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.RemoveListFormatting()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.IncreaseIndent()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.DecreaseIndent()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.Focus()
        {
            throw new NotImplementedException();
        }

        void IRichTextBoxService.InsertTextAtCaret(string text)
        {
            throw new NotImplementedException();
        }
    }
}

using CryptoBook.Interfaces;

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

using FontFamily = System.Windows.Media.FontFamily;
using Media = System.Windows.Media;
using Draving = System.Drawing;
using Controls = System.Windows.Controls;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;


namespace CryptoBook.Infrastructure
{
    public class BindableRichtextbox: Controls.RichTextBox, IRichTextBoxService
    {

        private TextRange last_Selection{get;set;}

        private readonly IRichTextBoxService service;


        public static readonly DependencyProperty CurrentForegroundProperty =
            DependencyProperty.Register("CurrentForeground", typeof(Media.Brush), typeof(BindableRichtextbox), new PropertyMetadata(Media.Brushes.Black));

        public static readonly DependencyProperty CurrentBackgroundProperty =
            DependencyProperty.Register("CurrentBackground", typeof(Media.Brush), typeof(BindableRichtextbox), new PropertyMetadata(Media.Brushes.Transparent));

        public static readonly DependencyProperty CurrentFontWeightProperty =
            DependencyProperty.Register("CurrentFontWeight", typeof(FontWeight), typeof(BindableRichtextbox), new PropertyMetadata(FontWeights.Normal));

        public static readonly DependencyProperty CurrentFontStyleProperty =
            DependencyProperty.Register("CurrentFontStyle", typeof(System.Windows.FontStyle), typeof(BindableRichtextbox), new PropertyMetadata(FontStyles.Normal));

        public static readonly DependencyProperty CurrentFontSizeProperty =
            DependencyProperty.Register("CurrentFontSize", typeof(double), typeof(BindableRichtextbox), new PropertyMetadata(12.0));

        public Media.Brush CurrentForeground
        {
            get => (Media.Brush)GetValue(CurrentForegroundProperty);
            set => SetValue(CurrentForegroundProperty, value);
        }

        public Media.Brush CurrentBackground
        {
            get => (Media.Brush)GetValue(CurrentBackgroundProperty);
            set => SetValue(CurrentBackgroundProperty, value);
        }

        public FontWeight CurrentFontWeight
        {
            get => (FontWeight)GetValue(CurrentFontWeightProperty);
            set => SetValue(CurrentFontWeightProperty, value);
        }

        public System.Windows.FontStyle CurrentFontStyle
        {
            get => (System.Windows.FontStyle)GetValue(CurrentFontStyleProperty);
            set => SetValue(CurrentFontStyleProperty, value);
        }

        public double CurrentFontSize
        {
            get => (double)GetValue(CurrentFontSizeProperty);
            set => SetValue(CurrentFontSizeProperty, value);
        }

        public BindableRichtextbox()
        {
            this.PreviewTextInput += OnPreviewTextInputApplyCurrentFormatting;
            service = this as IRichTextBoxService;
            this.LostFocus += OnLostFocusSaveSelection; 
        }

        private void OnLostFocusSaveSelection(object sender, RoutedEventArgs e)
        {
            last_Selection = new TextRange(Selection?.Start, Selection?.End);
        }

        private void OnPreviewTextInputApplyCurrentFormatting(object sender, TextCompositionEventArgs e)
        {
            // Apply current formatting properties to inserted text
            Selection.ApplyPropertyValue(TextElement.ForegroundProperty, CurrentForeground);
            Selection.ApplyPropertyValue(TextElement.BackgroundProperty, CurrentBackground);
            Selection.ApplyPropertyValue(TextElement.FontWeightProperty, CurrentFontWeight);
            Selection.ApplyPropertyValue(TextElement.FontStyleProperty, CurrentFontStyle);
            Selection.ApplyPropertyValue(TextElement.FontSizeProperty, CurrentFontSize);
        }

        // РџСѓР±Р»РёС‡РЅС‹Рµ СЃРІРѕР№СЃС‚РІР°, РґРѕСЃС‚СѓРїРЅС‹Рµ РЅР°РїСЂСЏРјСѓСЋ
        public new FlowDocument Document
        {
            get => base.Document;
            set => base.Document = value;
        }

        public new TextSelection Selection => base.Selection;

        public new bool CanUndo => base.CanUndo;
        public new bool CanRedo => base.CanRedo;

        public new TextPointer CaretPosition
        {
            get => base.CaretPosition;
            set => base.CaretPosition = value;
        }

        public new bool IsReadOnly
        {
            get => base.IsReadOnly;
            set => base.IsReadOnly = value;
        }

        public bool SpellCheckEnabled
        {
            get => SpellCheck.GetIsEnabled(this);
            set => SpellCheck.SetIsEnabled(this, value);
        }

        public new FontFamily FontFamily
        {
            get => base.FontFamily;
            set => base.FontFamily = value;
        }

        public new FontWeight FontWeight
        {
            get => base.FontWeight;
            set => base.FontWeight = value;
        }

        public new double FontSize
        {
            get => base.FontSize;
            set => base.FontSize = value;
        }

        void IRichTextBoxService.ApplyBold() 
        {
            service.RestoreSelection();
            TogglePropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
        }
        void IRichTextBoxService.ApplyItalic() => TogglePropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
        void IRichTextBoxService.ApplyUnderline() => Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
        void IRichTextBoxService.ApplyFontSize(double fontSize) => Selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
        void IRichTextBoxService.ApplyFontFamily(string fontFamily) => Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(fontFamily));
        void IRichTextBoxService.ApplyForegroundColor(Media.Color color) => Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
        void IRichTextBoxService.ApplyBackgroundColor(Media.Color color) => Selection.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(color));
        void IRichTextBoxService.ApplyTextAlignment(TextAlignment alignment) => Paragraph.SetTextAlignment(Selection.Start.Paragraph, alignment);
        void IRichTextBoxService.ApplyTextFormattingMode(TextFormattingMode mode) => TextOptions.SetTextFormattingMode(this, mode);
        void IRichTextBoxService.ApplyTextRenderingMode(TextRenderingMode mode) => TextOptions.SetTextRenderingMode(this, mode);
        void IRichTextBoxService.ApplyAcceptsTab(bool accept) => AcceptsTab = accept;
        void IRichTextBoxService.ApplyAcceptsReturn(bool accept) => AcceptsReturn = accept;
        void IRichTextBoxService.ApplyVerticalScrollBarVisibility(ScrollBarVisibility visibility) => VerticalScrollBarVisibility = visibility;
        void IRichTextBoxService.ApplyHorizontalScrollBarVisibility(ScrollBarVisibility visibility) => HorizontalScrollBarVisibility = visibility;
        void IRichTextBoxService.ApplyContextMenu(ContextMenu menu) => ContextMenu = menu;
        void IRichTextBoxService.ApplyDocumentEnabled(bool enabled) => IsDocumentEnabled = enabled;
        void IRichTextBoxService.ClearFormatting() => Selection.ClearAllProperties();


        void IRichTextBoxService.SelectAll() => base.SelectAll();
        void IRichTextBoxService.ClearSelection() => Selection.Select(Selection.End, Selection.End);
        string IRichTextBoxService.GetSelectedTextAsString() => Selection.Text;
        void IRichTextBoxService.ReplaceSelectedText(string text) => Selection.Text = text;

        void IRichTextBoxService.InsertHyperlink(string uri, string displayText)
        {
        }

        void IRichTextBoxService.InsertImage(string imagePath, double width, double height)
        {
            var image = new Controls.Image
            {
                Source = new ImageSourceConverter().ConvertFromString(imagePath) as ImageSource,
                Width = width > 0 ? width : Double.NaN,
                Height = height > 0 ? height : Double.NaN
            };

            var container = new InlineUIContainer(image, CaretPosition);
            CaretPosition = container.ElementEnd;
        }

        void IRichTextBoxService.InsertParagraph() => CaretPosition.Paragraph.SiblingBlocks.InsertAfter(CaretPosition.Paragraph, new Paragraph());
        void IRichTextBoxService.InsertLineBreak() => Selection.Text = Environment.NewLine;
        void IRichTextBoxService.InsertTable(int rows, int columns)
        {
        }

        string IRichTextBoxService.GetRtf()
        {
            var range = new TextRange(Document.ContentStart, Document.ContentEnd);
            using var stream = new MemoryStream();
            range.Save(stream, System.Windows.DataFormats.Rtf);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        void IRichTextBoxService.LoadRtf(string rtf)
        {
            var range = new TextRange(Document.ContentStart, Document.ContentEnd);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rtf));
            range.Load(stream, System.Windows.DataFormats.Rtf);
        }

        string IRichTextBoxService.GetPlainText()
        {
            var range = new TextRange(Document.ContentStart, Document.ContentEnd);
            return range.Text;
        }

        void IRichTextBoxService.LoadPlainText(string text)
        {
            Document.Blocks.Clear();
            Document.Blocks.Add(new Paragraph(new Run(text)));
        }

        void IRichTextBoxService.ClearDocument() => Document.Blocks.Clear();
        void IRichTextBoxService.ScrollToCaret() => ScrollToVerticalOffset(CaretPosition.GetCharacterRect(LogicalDirection.Forward).Top);
        void IRichTextBoxService.ScrollToEnd() => base.ScrollToEnd();
        void IRichTextBoxService.ScrollToStart() => base.ScrollToHome();
        void IRichTextBoxService.SetDocumentMargin(Thickness margin) => Document.PagePadding = margin;

        void IRichTextBoxService.Undo() => base.Undo();
        void IRichTextBoxService.Redo() => base.Redo();

        bool IRichTextBoxService.FindText(string searchText, bool matchCase, bool wholeWord)
        {
            var range = new TextRange(Document.ContentStart, Document.ContentEnd);
            var text = range.Text;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int index = text.IndexOf(searchText, comparison);
            if (index >= 0)
            {
                var start = GetTextPositionAtOffset(Document.ContentStart, index);
                var end = GetTextPositionAtOffset(Document.ContentStart, index + searchText.Length);
                Selection.Select(start, end);
                Focus();
                return true;
            }

            return false;
        }

        void IRichTextBoxService.ReplaceText(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            if (((IRichTextBoxService)this).FindText(searchText, matchCase, wholeWord))
            {
                ((IRichTextBoxService)this).ReplaceSelectedText(replaceText);
            }
        }

        void IRichTextBoxService.ReplaceAllText(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            while (((IRichTextBoxService)this).FindText(searchText, matchCase, wholeWord))
            {
                ((IRichTextBoxService)this).ReplaceSelectedText(replaceText);
            }
        }

        void IRichTextBoxService.ApplyBulletedList() => EditingCommands.ToggleBullets.Execute(null, this);
        void IRichTextBoxService.ApplyNumberedList() => EditingCommands.ToggleNumbering.Execute(null, this);
        void IRichTextBoxService.RemoveListFormatting()
        {
            EditingCommands.ToggleBullets.Execute(null, this);
            EditingCommands.ToggleNumbering.Execute(null, this);
        }

        void IRichTextBoxService.IncreaseIndent() => EditingCommands.IncreaseIndentation.Execute(null, this);
        void IRichTextBoxService.DecreaseIndent() => EditingCommands.DecreaseIndentation.Execute(null, this);
        void IRichTextBoxService.InsertTextAtCaret(string text)
        {
            Selection.Text = text;
            CaretPosition = Selection.End;
        }

        void IRichTextBoxService.Focus() => base.Focus();

        private void TogglePropertyValue(DependencyProperty property, object expectedValue)
        {
            var current = Selection.GetPropertyValue(property);
            var newValue = current != DependencyProperty.UnsetValue && current.Equals(expectedValue)
                ? DependencyProperty.UnsetValue
                : expectedValue;
            Selection.ApplyPropertyValue(property, newValue);
        }

        private TextPointer GetTextPositionAtOffset(TextPointer start, int offset)
        {
            TextPointer navigator = start;
            int chars = 0;

            while (navigator != null && chars < offset)
            {
                if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    int runLength = navigator.GetTextRunLength(LogicalDirection.Forward);
                    if (chars + runLength >= offset)
                        return navigator.GetPositionAtOffset(offset - chars);
                    chars += runLength;
                }
                navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
            }
            return navigator ?? start;
        }

        // Методы работы с буфером обмена
        void IRichTextBoxService.Copy()
        {
            if (!Selection.IsEmpty)
            {
                // Копируем выделенный текст (и форматирование) в буфер обмена
                ApplicationCommands.Copy.Execute(null, this);
            }
        }

        void IRichTextBoxService.Cut()
        {
            if (!Selection.IsEmpty && !IsReadOnly)
            {
                // Вырезаем выделенный текст (и форматирование) в буфер обмена
                ApplicationCommands.Cut.Execute(null, this);
            }
        }

        void IRichTextBoxService.Paste()
        {
            if (!IsReadOnly)
            {
                // Вставляем текст из буфера обмена в текущую позицию курсора
                ApplicationCommands.Paste.Execute(null, this);
            }
        }

        FlowDocument IRichTextBoxService.Document { get => base.Document; set => base.Document=value; }

        TextSelection IRichTextBoxService.Selection => base.Selection;

        bool IRichTextBoxService.CanUndo => base.CanUndo;

        bool IRichTextBoxService.CanRedo => base.CanRedo;

        TextPointer IRichTextBoxService.CaretPosition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        bool IRichTextBoxService.IsReadOnly { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        bool IRichTextBoxService.SpellCheckEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        FontFamily IRichTextBoxService.FontFamily { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        FontWeight IRichTextBoxService.FontWeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        double IRichTextBoxService.FontSize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        void IRichTextBoxService.RestoreSelection()
        {
            if(last_Selection != null)
            {
                CaretPosition = last_Selection.End;                
                Selection.Select(last_Selection.Start, last_Selection.End);

            } else
            {
                CaretPosition = Selection.End;
                Selection.Select(Selection.End, Selection.End);
            }
            Focus();
        }
    }
}

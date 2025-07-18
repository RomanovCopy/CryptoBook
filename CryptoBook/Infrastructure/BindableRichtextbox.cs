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
namespace CryptoBook.Infrastructure
{
    public class BindableRichtextbox: System.Windows.Controls.RichTextBox, IRichTextBoxService
    {
        private readonly IRichTextBoxService service;

        private FontWeight currentFontWeight { get; set; }

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

        public BindableRichtextbox()
        {
            DocumentContent = this.Document;
            service = (IRichTextBoxService)this;
            service.ApplyContextMenu(CreateContextMenu());
            service.ApplyDocumentEnabled(true);
            service.ApplyTextFormattingMode(TextFormattingMode.Ideal);
            service.ApplyAcceptsTab(true);
            service.ApplyAcceptsReturn(true);
        }

        private ContextMenu CreateContextMenu()
        {
            var contextMenu = new ContextMenu();

            // Добавление пунктов меню
            var copyItem = new MenuItem { Header = "Копировать" };
            copyItem.Click += (s, e) => this.Copy();
            contextMenu.Items.Add(copyItem);

            var pasteItem = new MenuItem { Header = "Вставить" };
            pasteItem.Click += (s, e) => this.Paste();
            contextMenu.Items.Add(pasteItem);

            var cutItem = new MenuItem { Header = "Вырезать" };
            cutItem.Click += (s, e) => this.Cut();
            contextMenu.Items.Add(cutItem);

            return contextMenu;
        }

        private static void OnDocumentContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rtb = (BindableRichtextbox)d;
            rtb.Document = e.NewValue as FlowDocument ?? new FlowDocument();
            rtb.TextChanged += OnTextChanged;
            rtb.KeyDown += OnKeyDown;
        }

        private static void OnTextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private static void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

            if(!char.IsControl((char)e.Key))
            {
                if(sender is BindableRichtextbox rtb && sender is IRichTextBoxService irtb)
                {
                    var range = new TextRange(rtb.CaretPosition, rtb.CaretPosition);
                    range.Text = e.Key.ToString();
                    range.ApplyPropertyValue(TextElement.FontWeightProperty, rtb.currentFontWeight);
                }
            }
        }

        TextSelection IRichTextBoxService.SelectedText => Selection;

        bool IRichTextBoxService.CanUndo => base.CanUndo;

        bool IRichTextBoxService.CanRedo => base.CanRedo;

        TextPointer IRichTextBoxService.CaretPosition
        {
            get => base.CaretPosition;
            set => base.CaretPosition = value;
        }

        bool IRichTextBoxService.IsReadOnly
        {
            get => base.IsReadOnly;
            set => base.IsReadOnly = value;
        }

        bool IRichTextBoxService.SpellCheckEnabled
        {
            get => base.SpellCheck.IsEnabled;
            set => base.SpellCheck.IsEnabled = value;
        }

        void IRichTextBoxService.ApplyBold()
        {
            if(Selection.Text.Length > 0)
                Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            else 
                currentFontWeight = FontWeights.Bold;

        }

        void IRichTextBoxService.ApplyItalic()
        {
            Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
        }

        void IRichTextBoxService.ApplyUnderline()
        {
            Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
        }

        void IRichTextBoxService.ApplyFontSize(double fontSize)
        {
            Selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
        }

        void IRichTextBoxService.ApplyFontFamily(string fontFamily)
        {
            Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily(fontFamily));
        }

        void IRichTextBoxService.ApplyForegroundColor(System.Drawing.Color color) => Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B)));

        void IRichTextBoxService.ApplyBackgroundColor(System.Drawing.Color color)
        {
            Selection.ApplyPropertyValue(TextElement.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B)));
        }

        void IRichTextBoxService.ApplyTextAlignment(TextAlignment alignment)
        {
            var paragraph = Selection.Start.Paragraph;
            if(paragraph != null)
                paragraph.TextAlignment = alignment;
        }

        void IRichTextBoxService.ApplyTextFormattingMode(TextFormattingMode mode)
        {
            TextOptions.SetTextFormattingMode(this, mode);
        }

        void IRichTextBoxService.ApplyTextRenderingMode(TextRenderingMode mode)
        {
            TextOptions.SetTextRenderingMode(this, mode);
        }

        void IRichTextBoxService.ApplyAcceptsTab(bool accept)
        {
            this.AcceptsTab = accept;
        }

        void IRichTextBoxService.ApplyAcceptsReturn(bool accept)
        {
            this.AcceptsReturn = accept;
        }

        void IRichTextBoxService.ApplyVerticalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            this.VerticalScrollBarVisibility = visibility;
        }

        void IRichTextBoxService.ApplyHorizontalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            this.HorizontalScrollBarVisibility = visibility;
        }

        void IRichTextBoxService.ApplyContextMenu(ContextMenu menu)
        {
            this.ContextMenu = menu;
        }

        void IRichTextBoxService.ApplyDocumentEnabled(bool enabled)
        {
            this.IsDocumentEnabled = enabled;
        }

        void IRichTextBoxService.ClearFormatting()
        {
            Selection.ClearAllProperties();
        }

        void IRichTextBoxService.SelectAll()
        {
            SelectAll();
        }

        void IRichTextBoxService.ClearSelection()
        {
            Selection.Select(DocumentContent.ContentStart, DocumentContent.ContentStart);
        }

        string IRichTextBoxService.GetSelectedTextAsString()
        {
            return Selection.Text;
        }

        void IRichTextBoxService.ReplaceSelectedText(string text)
        {
            Selection.Text = text;
        }

        void IRichTextBoxService.InsertHyperlink(string uri, string displayText)
        {
            try
            {
                var hyperlink = new Hyperlink(Selection.Start, Selection.End)
                {
                    NavigateUri = new Uri(uri),
                    ToolTip = uri
                };
                if(!string.IsNullOrEmpty(displayText))
                    hyperlink.Inlines.Clear();
                hyperlink.Inlines.Add(displayText);
            } catch(UriFormatException) { /* Обработка ошибки */ }
        }

        void IRichTextBoxService.InsertImage(string imagePath, double width, double height)
        {
            try
            {
                var document = DocumentContent;
                // Создаём изображение
                var image = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute))
                };
                if(width > 0)
                    image.Width = width;
                if(height > 0)
                    image.Height = height;

                // Создаём InlineUIContainer для встраивания изображения в текстовый поток
                var container = new InlineUIContainer(image);

                // Создаём новый параграф или используем существующий
                Paragraph paragraph;
                if(document.Blocks.Count == 0)
                {
                    // Если документ пустой, создаём новый параграф
                    paragraph = new Paragraph();
                    document.Blocks.Add(paragraph);
                } else
                {
                    // Используем последний параграф или создаём новый
                    paragraph = document.Blocks.OfType<Paragraph>().LastOrDefault() ?? new Paragraph();
                    if(!document.Blocks.Contains(paragraph))
                        document.Blocks.Add(paragraph);
                }

                // Добавляем изображение в параграф
                paragraph.Inlines.Add(container);
            } catch(Exception ex)
            {
                // Обработка ошибки
                System.Windows.MessageBox.Show($"Ошибка при вставке изображения: {ex.Message}");
            }
        }

        void IRichTextBoxService.InsertParagraph()
        {
            DocumentContent.Blocks.Add(new Paragraph());
            CaretPosition = DocumentContent.ContentEnd;
        }

        void IRichTextBoxService.InsertLineBreak()
        {
            var document = DocumentContent;
            var position = CaretPosition;
            if(document == null)
                throw new ArgumentNullException(nameof(document));
            if(position == null)
                throw new ArgumentNullException(nameof(position));

            // Получаем параграф
            Paragraph paragraph = position.Paragraph;
            if(paragraph == null)
            {
                paragraph = new Paragraph();
                document.Blocks.Add(paragraph);
                position = paragraph.ContentStart;
            }

            // Разделяем содержимое в позиции и вставляем LineBreak
            position = position.GetPositionAtOffset(0, LogicalDirection.Forward);
            paragraph.Inlines.Add(new LineBreak());
        }

        void IRichTextBoxService.InsertTable(int rows, int columns)
        {
            var table = new Table();
            var rowGroup = new TableRowGroup();
            for(int i = 0; i < rows; i++)
            {
                var row = new TableRow();
                for(int j = 0; j < columns; j++)
                    row.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                rowGroup.Rows.Add(row);
            }
            table.RowGroups.Add(rowGroup);
            DocumentContent.Blocks.Add(table);
        }

        string IRichTextBoxService.GetRtf()
        {
            using(var stream = new MemoryStream())
            {
                Selection.Save(stream, System.Windows.DataFormats.Rtf);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        void IRichTextBoxService.LoadRtf(string rtf)
        {
            using(var stream = new MemoryStream(Encoding.UTF8.GetBytes(rtf)))
            {
                Selection.Load(stream, System.Windows.DataFormats.Rtf);
            }
        }

        string IRichTextBoxService.GetPlainText()
        {
            return new TextRange(DocumentContent.ContentStart, DocumentContent.ContentEnd).Text;
        }

        void IRichTextBoxService.LoadPlainText(string text)
        {
            DocumentContent.Blocks.Clear();
            DocumentContent.Blocks.Add(new Paragraph(new Run(text)));
        }

        void IRichTextBoxService.ClearDocument()
        {
            DocumentContent.Blocks.Clear();
        }

        void IRichTextBoxService.ScrollToCaret()
        {
            BringIntoView(CaretPosition.GetCharacterRect(LogicalDirection.Forward));
        }

        void IRichTextBoxService.ScrollToEnd()
        {
            ScrollToEnd();
        }

        void IRichTextBoxService.ScrollToStart()
        {
            ScrollToHome();
        }

        void IRichTextBoxService.SetDocumentMargin(Thickness margin)
        {
            DocumentContent.PagePadding = margin;
        }

        void IRichTextBoxService.Undo()
        {
            base.Undo();
        }

        void IRichTextBoxService.Redo()
        {
            base.Redo();
        }

        bool IRichTextBoxService.FindText(string searchText, bool matchCase, bool wholeWord)
        {
            var range = new TextRange(Document.ContentStart, Document.ContentEnd);
            var position = range.Text.IndexOf(searchText, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            if(position >= 0)
            {
                var start = Document.ContentStart.GetPositionAtOffset(position);
                var end = start.GetPositionAtOffset(searchText.Length);
                Selection.Select(start, end);
                ((IRichTextBoxService)this).ScrollToCaret();
                return true;
            }
            return false;
        }

        void IRichTextBoxService.ReplaceText(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            if(((IRichTextBoxService)this).FindText(searchText, matchCase, wholeWord))
                Selection.Text = replaceText;
        }

        void IRichTextBoxService.ReplaceAllText(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            var range = new TextRange(DocumentContent.ContentStart, DocumentContent.ContentEnd);
            string text = range.Text;
            string newText = matchCase
                ? text.Replace(searchText, replaceText)
                : text.Replace(searchText, replaceText, StringComparison.OrdinalIgnoreCase);
            range.Text = newText;
        }

        void IRichTextBoxService.ApplyBulletedList()
        {
            EditingCommands.ToggleBullets.Execute(null, this);
        }

        void IRichTextBoxService.ApplyNumberedList()
        {
            EditingCommands.ToggleNumbering.Execute(null, this);
        }

        void IRichTextBoxService.RemoveListFormatting()
        {
            var paragraph = Selection.Start.Paragraph;
            if(paragraph != null && paragraph.Parent is List)
                DocumentContent.Blocks.Remove(paragraph.Parent as Block);
        }

        void IRichTextBoxService.IncreaseIndent()
        {
            EditingCommands.IncreaseIndentation.Execute(null, this);
        }

        void IRichTextBoxService.DecreaseIndent()
        {
            EditingCommands.DecreaseIndentation.Execute(null, this);
        }

        void IRichTextBoxService.Focus()
        {
            base.Focus();
        }

        void IRichTextBoxService.InsertTextAtCaret(string text)
        {
            Selection.Text = text;
        }



    }
}

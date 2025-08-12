using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

using Media = System.Windows.Media;
using Draving = System.Drawing;

using Controls = System.Windows.Controls;
using FontStyle = System.Windows.FontStyle;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.Services
{
    public class RichTextBoxService: Controls.RichTextBox, IRichTextBoxService
    {
        private readonly ILifetimeScope scope;

        private TextRange last_Selection;

        FlowDocument IRichTextBoxService.Document => this.Document;

        Controls.RichTextBox IRichTextBoxService.Service => this;
        TextSelection IRichTextBoxService.Selection => this.Selection;
        TextPointer IRichTextBoxService.CaretPosition
        {
            get => this.CaretPosition;
            set => this.CaretPosition = value;
        }

        Media.Brush IRichTextBoxService.CaretBrush { get => this.CaretBrush; set => this.CaretBrush=value; }
        Media.Brush IRichTextBoxService.BackGround { get => this.Background; set => this.Background=value; }


        bool IRichTextBoxService.IsReadOnly
        {
            get => this.IsReadOnly;
            set => this.IsReadOnly = value;
        }
        bool IRichTextBoxService.SpellCheckEnabled
        {
            get => this.SpellCheck.IsEnabled;
            set => this.SpellCheck.IsEnabled = value;
        }
        bool IRichTextBoxService.CanUndo => this.CanUndo;
        bool IRichTextBoxService.CanRedo => this.CanRedo;


        public RichTextBoxService(ILifetimeScope scope)
        {
            this.scope = scope;
            this.LostFocus += RichTextBoxService_LostFocus;
            this.PreviewKeyDown += RichTextBoxService_PreviewKeyDown;
            InitializeDocument();
            this.AcceptsTab = true; // Разрешаем табы
        }


        //перенос строки без создания нового параграфа
        private void RichTextBoxService_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true; // отменяем стандартный Enter

                var caret = this.CaretPosition;

                // Вставляем перенос строки
                caret.InsertLineBreak();

                // Вставляем пустой Run, чтобы была точка для курсора
                var emptyRun = new Run("");
                caret.Paragraph.Inlines.Add(emptyRun);

                // Ставим курсор в этот Run
                this.CaretPosition = emptyRun.ContentStart;
            }
        }

        void IRichTextBoxService.Focus() => this.Focus();
        void IRichTextBoxService.ScrollToCaret() => this.ScrollToVerticalOffset(this.VerticalOffset);
        void IRichTextBoxService.ScrollToStart() => this.ScrollToHome();
        void IRichTextBoxService.ScrollToEnd() => this.ScrollToEnd();
        void IRichTextBoxService.Copy() => this.Copy();
        void IRichTextBoxService.Cut() => this.Cut();
        void IRichTextBoxService.Paste() => this.Paste();
        void IRichTextBoxService.SelectAll() => this.SelectAll();
        void IRichTextBoxService.ClearSelection() => this.Selection.Select(this.CaretPosition, this.CaretPosition);
        void IRichTextBoxService.RestoreSelection()
        {
            if(last_Selection != null)
            {
                this.CaretPosition = last_Selection.End;
                this.Selection.Select(last_Selection.Start, last_Selection.End);

            } else
            {
                this.CaretPosition = this.Selection.End;
                this.Selection.Select(this.Selection.End, this.Selection.End);
            }
            this.Focus();
        }
        void IRichTextBoxService.InsertTextAtCaret(string text) => this.CaretPosition.InsertTextInRun(text);
        void IRichTextBoxService.Undo() => this.Undo();
        void IRichTextBoxService.Redo() => this.Redo();
        void IRichTextBoxService.ApplyVerticalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            this.VerticalScrollBarVisibility = visibility;
        }
        void IRichTextBoxService.ApplyHorizontalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            this.HorizontalScrollBarVisibility = visibility;
        }
        void IRichTextBoxService.ApplyContextMenu(ContextMenu menu) => this.ContextMenu = menu;
        void IRichTextBoxService.ApplyAcceptsTab(bool accept) => this.AcceptsTab = accept;
        void IRichTextBoxService.ApplyAcceptsReturn(bool accept) => this.AcceptsReturn = accept;


        public double GetFontSizeInSelection()
        {
            if(Selection.IsEmpty)
                return (double)(Selection.GetPropertyValue(System.Windows.Documents.TextElement.FontSizeProperty) ?? 12.0);

            TextPointer start = Selection.Start;
            TextPointer end = Selection.End;

            var sizes = new List<double>();
            var position = start;

            while(position != null && position.CompareTo(end) < 0)
            {
                if(position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text ||
                    position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart)
                {
                    var element = position.Parent as System.Windows.Documents.TextElement;
                    if(element != null)
                    {
                        var sizeObj = element.GetValue(System.Windows.Documents.TextElement.FontSizeProperty);
                        if(sizeObj is double size)
                            sizes.Add(size);
                    }
                }
                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }
            if(sizes.Count == 0)
                return 12.0;
            return sizes.Count > 1 ? 0 : sizes[0];
        }
        private object GetTextPropertiesInCaretPosition(DependencyProperty property)
        {
            TextPointer caret = this.CaretPosition.GetInsertionPosition(LogicalDirection.Backward);
            if(caret == null)
                return DependencyProperty.UnsetValue;

            TextRange range = new TextRange(caret, caret);
            return range.GetPropertyValue(property);
        }

        private void RichTextBoxService_LostFocus(object sender, RoutedEventArgs e)
        {
            last_Selection = new TextRange(Selection?.Start, Selection?.End);
        }


        double IRichTextBoxService.GetFontSizeInSelection()
        {
            throw new NotImplementedException();
        }




        private void InitializeDocument()
        {
            var document = this.Document;

            if(document == null)
                throw new InvalidOperationException("Document cannot be null. Ensure that the RichTextBox is properly initialized.");
            document.PagePadding = new Thickness(10, 20, 10, 20);
            // Создаём пустой абзац, если документ совсем пустой
            if(document.Blocks.Count == 0)
            {
                document.Blocks.Add(new Paragraph());
            }

            Paragraph firstParagraph = document.Blocks.FirstBlock as Paragraph;
            if(firstParagraph == null)
            {
                firstParagraph = new Paragraph();
                document.Blocks.InsertBefore(document.Blocks.FirstBlock, firstParagraph);
            }

            // Создаём новый Run и добавляем в начало абзаца
            Run newRun = new("");
            if(firstParagraph.Inlines.FirstInline != null)
                firstParagraph.Inlines.InsertBefore(firstParagraph.Inlines.FirstInline, newRun);
            else
                firstParagraph.Inlines.Add(newRun); // если Inlines пустой

            foreach(var block in document.Blocks)
            {
                if(block is Paragraph paragraph)
                {
                    paragraph.ClearValue(Paragraph.LineHeightProperty);
                    paragraph.ClearValue(Paragraph.LineStackingStrategyProperty);
                }
            }

            document.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            document.LineHeight = 15;

            // Устанавливаем каретку в начало нового Run
            CaretPosition = newRun.ContentStart;
            Focus();
        }


    }
}

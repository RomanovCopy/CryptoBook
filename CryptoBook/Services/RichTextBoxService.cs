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
using System.Runtime.CompilerServices;
using System.CodeDom;
using System.Collections.Generic;

namespace CryptoBook.Services
{
    public class RichTextBoxService: Controls.RichTextBox, IRichTextBoxService
    {

        private TextRange last_Selection;
        private IParagraphFactory paragraphFactory;
        private readonly Dictionary<DependencyProperty, object?> typingProperties = new();
        private Paragraph? typingAnchorParagraph;
        private int typingAnchorTextOffset;
        private static readonly DependencyProperty[] inheritedTypingProperties =
        [
            TextElement.FontFamilyProperty,
            TextElement.FontSizeProperty,
            TextElement.FontWeightProperty,
            TextElement.FontStyleProperty,
            TextElement.FontStretchProperty,
            TextElement.ForegroundProperty,
            TextElement.BackgroundProperty,
            Inline.TextDecorationsProperty,
            Inline.BaselineAlignmentProperty
        ];

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


        public RichTextBoxService( IParagraphFactory paragraphFactory)
        {
            this.paragraphFactory = paragraphFactory;
            this.LostFocus += RichTextBoxService_LostFocus;
            this.PreviewKeyDown += RichTextBoxService_PreviewKeyDown;
            this.PreviewTextInput += RichTextBoxService_PreviewTextInput;
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
        void IRichTextBoxService.SetTypingProperty(DependencyProperty property, object? value)
        {
            if(property == null)
                throw new ArgumentNullException(nameof(property));

            if(!IsTypingAnchorAtCaret())
                typingProperties.Clear();

            RememberTypingAnchor();
            typingProperties[property] = value;
        }
        void IRichTextBoxService.InsertTextAtCaret(string text)
        {
            if(string.IsNullOrEmpty(text))
                return;

            var start = this.CaretPosition;
            bool applyTypingProperties = typingProperties.Count > 0 && IsTypingAnchorAtCaret();

            if(applyTypingProperties)
            {
                var inheritedValues = inheritedTypingProperties.ToDictionary(
                    property => property,
                    property => GetEffectiveValue(start, property));

                var run = new Run(text, start);
                foreach(var (property, value) in inheritedValues)
                {
                    if(!ReferenceEquals(value, DependencyProperty.UnsetValue))
                        run.SetValue(property, value);
                }
                foreach(var (property, value) in typingProperties)
                    run.SetValue(property, value);

                this.CaretPosition = run.ContentEnd;
            } else
            {
                typingProperties.Clear();
                start.InsertTextInRun(text);
                this.CaretPosition = start.GetPositionAtOffset(text.Length, LogicalDirection.Forward) ?? start;
            }

            this.Selection.Select(this.CaretPosition, this.CaretPosition);
            RememberTypingAnchor();
        }
        void IRichTextBoxService.ClearDocument()
        {
            this.BeginChange();
            try
            {
                this.SelectAll();
                this.Selection.Text = string.Empty;

                var caret = this.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
                this.CaretPosition = caret;
                this.Selection.Select(caret, caret);

                typingProperties.Clear();
                RememberTypingAnchor();
            } finally
            {
                this.EndChange();
            }
        }
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

        void IRichTextBoxService.BeginChange()=>this.BeginChange();
        void IRichTextBoxService.EndChange() => this.EndChange();

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
        private void RichTextBoxService_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if(string.IsNullOrEmpty(e.Text) || typingProperties.Count == 0 || !IsTypingAnchorAtCaret())
                return;

            e.Handled = true;
            ((IRichTextBoxService)this).InsertTextAtCaret(e.Text);
        }
        private bool IsTypingAnchorAtCaret()
        {
            var paragraph = this.CaretPosition.Paragraph;
            if(typingAnchorParagraph == null || !ReferenceEquals(typingAnchorParagraph, paragraph))
                return false;

            return GetTextOffset(paragraph, this.CaretPosition) == typingAnchorTextOffset;
        }
        private void RememberTypingAnchor()
        {
            typingAnchorParagraph = this.CaretPosition.Paragraph;
            typingAnchorTextOffset = GetTextOffset(typingAnchorParagraph, this.CaretPosition);
        }
        private static int GetTextOffset(Paragraph? paragraph, TextPointer position)
        {
            if(paragraph == null)
                return 0;

            return new TextRange(paragraph.ContentStart, position).Text.Length;
        }
        private object GetEffectiveValue(TextPointer position, DependencyProperty property)
        {
            var value = new TextRange(position, position).GetPropertyValue(property);
            if(!ReferenceEquals(value, DependencyProperty.UnsetValue))
                return value;

            if(position.Parent is TextElement parent)
                return parent.GetValue(property);

            var backward = position.GetAdjacentElement(LogicalDirection.Backward) as TextElement;
            if(backward != null)
                return backward.GetValue(property);

            var forward = position.GetAdjacentElement(LogicalDirection.Forward) as TextElement;
            return forward?.GetValue(property) ?? this.Document.GetValue(property);
        }
        double IRichTextBoxService.GetFontSizeInSelection()
        {
            throw new NotImplementedException();
        }
        private void InitializeDocument()
        {
            var document = this.Document;
            document.Background=System.Windows.Media.Brushes.Transparent;
            if(document == null)
                throw new InvalidOperationException("Document cannot be null. Ensure that the RichTextBox is properly initialized.");
            document.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            document.LineHeight = 20;
            Run newRun = new("     ");
            newRun.Foreground = System.Windows.Media.Brushes.Black;
            newRun.Background= System.Windows.Media.Brushes.Transparent;
            var newParagraph = paragraphFactory.Create();

            document.PagePadding = new Thickness(10, 20, 10, 20);
            document.Blocks.Clear();
            document.Blocks.Add((Paragraph)newParagraph);

            newParagraph.Inlines.Add(newRun);

            // Устанавливаем каретку в начало нового Run
            CaretPosition = newRun.ContentStart;
            Focus();
        }



    }
}

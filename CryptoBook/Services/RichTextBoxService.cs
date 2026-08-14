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
using Drawing = System.Drawing;

using Controls = System.Windows.Controls;
using FontStyle = System.Windows.FontStyle;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Runtime.CompilerServices;
using System.CodeDom;
namespace CryptoBook.Services
{
    public class RichTextBoxService:
        Controls.RichTextBox,
        IRichTextBoxService,
        IDocumentReplacementNotifier
    {

        private TextRange? lastSelection;
        private IParagraphFactory paragraphFactory;
        private readonly IUriNavigationService uriNavigationService;
        private readonly IDocumentAppearanceDefaults appearanceDefaults;
        private readonly Dictionary<DependencyProperty, object?> typingProperties = new();
        private readonly ConditionalWeakTable<Run, object> typingRuns = new();
        private static readonly object typingRunMarker = new();
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
            set
            {
                if(typingProperties.Count > 0 &&
                   !AreSamePosition(this.CaretPosition, value))
                {
                    typingProperties.Clear();
                }

                this.CaretPosition = value;
            }
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

        public event EventHandler? DocumentReplaced;


        public RichTextBoxService(
            IParagraphFactory paragraphFactory,
            IUriNavigationService uriNavigationService,
            IDocumentAppearanceDefaults appearanceDefaults)
        {
            this.paragraphFactory = paragraphFactory ??
                throw new ArgumentNullException(nameof(paragraphFactory));
            this.uriNavigationService = uriNavigationService ??
                throw new ArgumentNullException(nameof(uriNavigationService));
            this.appearanceDefaults = appearanceDefaults ??
                throw new ArgumentNullException(nameof(appearanceDefaults));
            var hyperlinkStyle = new Style(typeof(Hyperlink));
            hyperlinkStyle.Setters.Add(
                new Setter(Hyperlink.CursorProperty, System.Windows.Input.Cursors.Hand));
            this.Resources[typeof(Hyperlink)] = hyperlinkStyle;

            // Formatting toolbars and their popups take keyboard focus away
            // from the editor. Keep the selected range visible while those
            // controls are being used.
            this.IsInactiveSelectionHighlightEnabled = true;

            this.LostFocus += RichTextBoxService_LostFocus;
            this.PreviewKeyDown += RichTextBoxService_PreviewKeyDown;
            this.PreviewTextInput += RichTextBoxService_PreviewTextInput;
            this.PreviewMouseLeftButtonDown += RichTextBoxService_PreviewMouseLeftButtonDown;
            this.AddHandler(
                Hyperlink.RequestNavigateEvent,
                new RequestNavigateEventHandler(RichTextBoxService_RequestNavigate));
            InitializeDocument();
            this.IsDocumentEnabled = true;
            this.AcceptsTab = true; // Разрешаем табы
        }


        // Shift+Enter — перенос строки внутри текущего абзаца. Обычный Enter
        // не перехватываем: WPF сам создаёт новый Paragraph или ListItem.
        private void RichTextBoxService_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(IsCaretNavigationKey(e.Key) ||
               (e.Key == Key.A &&
                Keyboard.Modifiers.HasFlag(ModifierKeys.Control)))
            {
                typingProperties.Clear();
            }

            if(e.Key == Key.Escape && TryDismissSelection())
            {
                typingProperties.Clear();
                e.Handled = true;
                return;
            }

            var isEnter = e.Key == Key.Enter || e.Key == Key.Return;
            if(!isEnter || !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                return;

            e.Handled = true;
            EditingCommands.EnterLineBreak.Execute(null, this);
        }

        void IRichTextBoxService.Focus() => this.Focus();
        void IRichTextBoxService.ScrollToCaret() => this.ScrollToVerticalOffset(this.VerticalOffset);
        void IRichTextBoxService.ScrollToStart() => this.ScrollToHome();
        void IRichTextBoxService.ScrollToEnd() => this.ScrollToEnd();
        void IRichTextBoxService.Copy() => this.Copy();
        void IRichTextBoxService.Cut() => this.Cut();
        void IRichTextBoxService.Paste() => this.Paste();
        void IRichTextBoxService.SelectAll()
        {
            typingProperties.Clear();
            this.SelectAll();
        }
        void IRichTextBoxService.ClearSelection()
        {
            lastSelection = null;
            this.Selection.Select(this.CaretPosition, this.CaretPosition);
        }
        void IRichTextBoxService.RestoreSelection()
        {
            // A toolbar control can collapse RichTextBox.Selection after the
            // editor has lost focus. Restore only a collapsed selection: a
            // non-empty live selection may have been set programmatically and
            // must take precedence over an older snapshot.
            if(!this.Selection.IsEmpty ||
               lastSelection == null ||
               lastSelection.Start.CompareTo(lastSelection.End) == 0)
                return;

            this.CaretPosition = lastSelection.End;
            this.Selection.Select(lastSelection.Start, lastSelection.End);
        }
        void IRichTextBoxService.SetTypingProperty(DependencyProperty property, object? value)
        {
            if(property == null)
                throw new ArgumentNullException(nameof(property));

            typingProperties[property] = value;
        }
        void IRichTextBoxService.InsertTextAtCaret(string text)
        {
            if(this.IsReadOnly || string.IsNullOrEmpty(text))
                return;

            this.BeginChange();
            try
            {
                var start = this.CaretPosition;
                if(!this.Selection.IsEmpty)
                {
                    this.Selection.Text = string.Empty;
                    start = this.Selection.Start.GetInsertionPosition(
                        LogicalDirection.Forward);
                    this.CaretPosition = start;
                    this.Selection.Select(start, start);
                }

                bool applyTypingProperties = typingProperties.Count > 0;
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

                    typingRuns.Add(run, typingRunMarker);
                    run = MergeWithPreviousTypingRun(run);
                    this.CaretPosition = run.ContentEnd;
                } else
                {
                    start.InsertTextInRun(text);
                    this.CaretPosition = start.GetPositionAtOffset(
                        text.Length,
                        LogicalDirection.Forward) ?? start;
                }

                this.Selection.Select(this.CaretPosition, this.CaretPosition);
            } finally
            {
                this.EndChange();
            }
        }
        void IRichTextBoxService.ClearDocument()
        {
            this.BeginChange();
            try
            {
                var paragraph = paragraphFactory.Create();
                paragraph.Margin = new Thickness(0);
                paragraph.Element.ClearValue(Paragraph.LineHeightProperty);
                paragraph.LineStackingStrategy =
                    LineStackingStrategy.MaxHeight;

                this.Document.LineHeight = double.NaN;
                this.Document.LineStackingStrategy =
                    LineStackingStrategy.MaxHeight;
                this.Document.Blocks.Clear();
                this.Document.Blocks.Add(paragraph.Element);

                var caret = paragraph.Element.ContentStart
                    .GetInsertionPosition(LogicalDirection.Forward);
                this.CaretPosition = caret;
                this.Selection.Select(caret, caret);

                lastSelection = null;

                typingProperties.Clear();
            } finally
            {
                this.EndChange();
            }
        }

        void IRichTextBoxService.ReplaceDocument(FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            var paperBrush = CreateBrush(appearanceDefaults.PaperColor);
            var textBrush = CreateBrush(appearanceDefaults.TextColor);
            document.Background = paperBrush;
            document.Foreground = textBrush;
            document.LineStackingStrategy = LineStackingStrategy.MaxHeight;
            document.LineHeight = double.NaN;
            DocumentPageLayout.Apply(document);

            Document = document;
            Background = paperBrush;
            Foreground = textBrush;
            CaretBrush = textBrush;

            var caret = document.ContentStart.GetInsertionPosition(
                LogicalDirection.Forward);
            CaretPosition = caret;
            Selection.Select(caret, caret);
            lastSelection = null;
            typingProperties.Clear();
            DocumentReplaced?.Invoke(this, EventArgs.Empty);
        }

        bool IRichTextBoxService.HasEmptyParagraphs() =>
            HasRemovableEmptyParagraphs(this.Document.Blocks);

        int IRichTextBoxService.RemoveEmptyParagraphs()
        {
            int removed;
            this.BeginChange();
            try
            {
                removed = RemoveEmptyParagraphs(this.Document.Blocks);
                if(this.Document.Blocks.Count == 0)
                {
                    var paragraph = paragraphFactory.Create();
                    paragraph.Margin = new Thickness(0);
                    this.Document.Blocks.Add(paragraph.Element);
                }

                if(this.CaretPosition.Paragraph is null)
                {
                    TextPointer caret = this.Document.ContentStart
                        .GetInsertionPosition(LogicalDirection.Forward);
                    this.CaretPosition = caret;
                    this.Selection.Select(caret, caret);
                }

                typingProperties.Clear();
            } finally
            {
                this.EndChange();
            }

            return removed;
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
            var value = Selection.GetPropertyValue(TextElement.FontSizeProperty);
            if(value is double size)
                return size;

            // Ноль обозначает смешанные размеры в выделении.
            return ReferenceEquals(value, DependencyProperty.UnsetValue)
                ? 0
                : Document.FontSize;
        }
        private object GetTextPropertiesInCaretPosition(DependencyProperty property)
        {
            TextPointer caret = this.CaretPosition.GetInsertionPosition(LogicalDirection.Backward);
            if(caret == null)
                return DependencyProperty.UnsetValue;

            TextRange range = new TextRange(caret, caret);
            return range.GetPropertyValue(property);
        }

        private static bool HasRemovableEmptyParagraphs(
            BlockCollection blocks)
        {
            Block[] items = blocks.ToList().ToArray();
            if(items.Length > 1 &&
               items.Any(block =>
                   block is Paragraph paragraph &&
                   IsEmptyParagraph(paragraph)))
            {
                return true;
            }

            return items.Any(block => block switch
            {
                Section section =>
                    HasRemovableEmptyParagraphs(section.Blocks),
                System.Windows.Documents.List list =>
                    list.ListItems.Any(item =>
                        HasRemovableEmptyParagraphs(item.Blocks)),
                _ => false
            });
        }

        private static int RemoveEmptyParagraphs(
            BlockCollection blocks)
        {
            int removed = 0;

            foreach(Block block in blocks.ToList())
            {
                if(block is Section section)
                {
                    removed += RemoveEmptyParagraphs(section.Blocks);
                }
                else if(block is System.Windows.Documents.List list)
                {
                    foreach(ListItem item in list.ListItems)
                        removed += RemoveEmptyParagraphs(item.Blocks);
                }
            }

            if(blocks.Count <= 1)
                return removed;

            foreach(Paragraph paragraph in blocks
                .OfType<Paragraph>()
                .Where(IsEmptyParagraph)
                .ToList())
            {
                if(blocks.Count <= 1)
                    break;

                blocks.Remove(paragraph);
                removed++;
            }

            return removed;
        }

        private static bool IsEmptyParagraph(Paragraph paragraph)
        {
            if(!string.IsNullOrWhiteSpace(paragraph.Name) ||
               paragraph.Tag is not null)
            {
                return false;
            }

            return paragraph.Inlines.All(IsEmptyInline);
        }

        private static bool IsEmptyInline(Inline inline)
        {
            if(!string.IsNullOrWhiteSpace(inline.Name) ||
               inline.Tag is not null)
            {
                return false;
            }

            return inline switch
            {
                Run run => string.IsNullOrWhiteSpace(
                    (run.Text ?? string.Empty)
                    .Replace("\u200B", string.Empty)
                    .Replace("\u2060", string.Empty)),
                LineBreak => true,
                Hyperlink => false,
                Span span => span.Inlines.All(IsEmptyInline),
                _ => false
            };
        }

        private void RichTextBoxService_LostFocus(object sender, RoutedEventArgs e)
        {
            lastSelection = Selection.IsEmpty
                ? null
                : new TextRange(Selection.Start, Selection.End);
        }
        private void RichTextBoxService_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if(this.IsReadOnly ||
               string.IsNullOrEmpty(e.Text) ||
               typingProperties.Count == 0)
            {
                return;
            }

            e.Handled = true;
            ((IRichTextBoxService)this).InsertTextAtCaret(e.Text);
        }

        private void RichTextBoxService_PreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            typingProperties.Clear();
            var position = GetPositionFromPoint(e.GetPosition(this), snapToText: true);
            DismissSelectionIfOutside(position);

            var hyperlink = FindHyperlink(position);
            if(hyperlink?.NavigateUri == null)
                return;

            e.Handled = TryNavigate(hyperlink.NavigateUri);
        }

        internal void DismissSelectionIfOutside(TextPointer? position)
        {
            if(position == null)
                return;

            TextRange? range = !Selection.IsEmpty
                ? new TextRange(Selection.Start, Selection.End)
                : lastSelection;
            if(range == null)
                return;

            if(range.Start.CompareTo(range.End) == 0)
            {
                lastSelection = null;
                return;
            }

            bool isInside = position.CompareTo(range.Start) >= 0 &&
                            position.CompareTo(range.End) < 0;
            if(isInside)
            {
                lastSelection = null;
                return;
            }

            var caret = position.GetInsertionPosition(LogicalDirection.Forward);
            this.CaretPosition = caret;
            lastSelection = null;
            this.Selection.Select(caret, caret);
        }

        private bool TryDismissSelection()
        {
            bool hasLiveSelection = !this.Selection.IsEmpty;
            bool hasSavedSelection = lastSelection != null &&
                lastSelection.Start.CompareTo(lastSelection.End) != 0;
            if(!hasLiveSelection && !hasSavedSelection)
                return false;

            var caret = hasLiveSelection
                ? this.CaretPosition
                : lastSelection!.End;
            this.CaretPosition = caret;
            lastSelection = null;
            this.Selection.Select(caret, caret);
            return true;
        }

        private void RichTextBoxService_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if(e.Uri != null)
                e.Handled = TryNavigate(e.Uri);
        }

        private bool TryNavigate(Uri uri)
        {
            if(!uri.IsAbsoluteUri)
            {
                var anchorName = Uri.UnescapeDataString(
                    uri.OriginalString.TrimStart('#'));
                var target = FindNamedTextElement(anchorName);
                if(target != null)
                {
                    var position = target.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
                    this.Selection.Select(position, position);
                    this.CaretPosition = position;
                    target.BringIntoView();
                    this.Focus();
                    return true;
                }
                return false;
            }

            uriNavigationService.TryOpen(uri);
            return true;
        }

        private static Hyperlink? FindHyperlink(TextPointer? position)
        {
            if(position == null)
                return null;

            return FindAncestorHyperlink(position.Parent) ??
                   FindAncestorHyperlink(
                       position.GetAdjacentElement(LogicalDirection.Forward) as DependencyObject) ??
                   FindAncestorHyperlink(
                       position.GetAdjacentElement(LogicalDirection.Backward) as DependencyObject);
        }

        private static Hyperlink? FindAncestorHyperlink(DependencyObject? current)
        {
            while(current != null)
            {
                if(current is Hyperlink hyperlink)
                    return hyperlink;

                current = current is FrameworkContentElement element
                    ? element.Parent
                    : null;
            }

            return null;
        }

        private TextElement? FindNamedTextElement(string name)
        {
            var seen = new HashSet<TextElement>();
            for(var position = this.Document.ContentStart;
                position != null && position.CompareTo(this.Document.ContentEnd) < 0;
                position = position.GetNextContextPosition(LogicalDirection.Forward))
            {
                if(position.GetAdjacentElement(LogicalDirection.Forward) is TextElement element &&
                   !string.IsNullOrWhiteSpace(element.Name) &&
                   string.Equals(element.Name, name, StringComparison.Ordinal) &&
                   seen.Add(element))
                {
                    return element;
                }
            }

            return null;
        }
        private static bool IsCaretNavigationKey(Key key) => key is
            Key.Left or
            Key.Right or
            Key.Up or
            Key.Down or
            Key.Home or
            Key.End or
            Key.PageUp or
            Key.PageDown;

        private static bool AreSamePosition(
            TextPointer current,
            TextPointer candidate)
        {
            try
            {
                return current.CompareTo(candidate) == 0;
            } catch(ArgumentException)
            {
                return false;
            }
        }

        private Run MergeWithPreviousTypingRun(Run run)
        {
            if(run.PreviousInline is not Run previous ||
               !typingRuns.TryGetValue(previous, out _) ||
               !HaveEquivalentTypingFormatting(previous, run))
            {
                return run;
            }

            var text = run.Text;
            RemoveInline(run);
            typingRuns.Remove(run);
            previous.Text += text;
            return previous;
        }

        private static bool HaveEquivalentTypingFormatting(
            Run left,
            Run right)
        {
            var leftValues = GetComparableLocalValues(left);
            var rightValues = GetComparableLocalValues(right);
            if(leftValues.Count != rightValues.Count)
                return false;

            foreach(var (property, leftValue) in leftValues)
            {
                if(!rightValues.TryGetValue(property, out var rightValue) ||
                   !AreEquivalentFormattingValues(leftValue, rightValue))
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<DependencyProperty, object?>
            GetComparableLocalValues(Run run)
        {
            var values = new Dictionary<DependencyProperty, object?>();
            var enumerator = run.GetLocalValueEnumerator();
            while(enumerator.MoveNext())
            {
                var entry = enumerator.Current;
                if(entry.Property != Run.TextProperty)
                    values[entry.Property] = entry.Value;
            }

            return values;
        }

        private static bool AreEquivalentFormattingValues(
            object? left,
            object? right)
        {
            if(ReferenceEquals(left, right) || Equals(left, right))
                return true;

            return left is TextDecorationCollection leftDecorations &&
                   right is TextDecorationCollection rightDecorations &&
                   leftDecorations.SequenceEqual(rightDecorations);
        }

        private static void RemoveInline(Inline inline)
        {
            switch(inline.Parent)
            {
                case Paragraph paragraph:
                    paragraph.Inlines.Remove(inline);
                    break;
                case Span span:
                    span.Inlines.Remove(inline);
                    break;
            }
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
        double IRichTextBoxService.GetFontSizeInSelection() => GetFontSizeInSelection();
        private void InitializeDocument()
        {
            var document = this.Document;
            if(document == null)
                throw new InvalidOperationException("Document cannot be null. Ensure that the RichTextBox is properly initialized.");
            // Бумага и текст являются частью документа, поэтому смена темы
            // интерфейса не должна менять их цвета.
            var paperBrush = CreateBrush(appearanceDefaults.PaperColor);
            var textBrush = CreateBrush(appearanceDefaults.TextColor);
            document.Background = paperBrush;
            document.Foreground = textBrush;
            this.Background = paperBrush;
            this.Foreground = textBrush;
            this.CaretBrush = textBrush;
            this.SetResourceReference(
                BorderBrushProperty,
                "CurrentBorderColor");
            // Автоматическая высота учитывает реальные метрики выбранного шрифта.
            // MaxHeight не позволяет глифам и маркерам списка перекрывать соседнюю строку.
            document.LineStackingStrategy = LineStackingStrategy.MaxHeight;
            document.LineHeight = double.NaN;
            var newParagraph = paragraphFactory.Create();
            newParagraph.Margin = new Thickness(0);

            DocumentPageLayout.Apply(document);
            document.Blocks.Clear();
            document.Blocks.Add((Paragraph)newParagraph);

            CaretPosition = newParagraph.Element.ContentStart
                .GetInsertionPosition(LogicalDirection.Forward);
            Focus();
        }

        private static Media.SolidColorBrush CreateBrush(Drawing.Color color) =>
            new(Media.Color.FromArgb(color.A, color.R, color.G, color.B));



    }
}

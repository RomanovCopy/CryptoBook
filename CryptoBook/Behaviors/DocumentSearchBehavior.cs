using CryptoBook.Adorners;
using CryptoBook.MyControls;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using Microsoft.Xaml.Behaviors;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

using TextBox = System.Windows.Controls.TextBox;
using RichTextBox = System.Windows.Controls.RichTextBox;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace CryptoBook.Behaviors
{
    public sealed class DocumentSearchBehavior: Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty SearchProperty = Register(
            nameof(Search), typeof(DocumentSearchViewModel));
        public static readonly DependencyProperty EditorProperty = Register(
            nameof(Editor), typeof(TextBoxBase));
        public static readonly DependencyProperty AlternateEditorProperty = Register(
            nameof(AlternateEditor), typeof(TextBoxBase));
        public static readonly DependencyProperty SearchBoxProperty = Register(
            nameof(SearchBox), typeof(TextBox));

        private DocumentSearchViewModel? subscribedSearch;
        private readonly List<TextBoxBase> subscribedEditors = [];
        private readonly DispatcherTimer refreshTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        private IReadOnlyList<TextRange> richMatches = [];
        private IReadOnlyList<TextSearchMatch> textMatches = [];
        private TextBoxBase? activeEditor;
        private DocumentSearchAdorner? adorner;
        private AdornerLayer? adornerLayer;
        private bool updatingResults;
        private bool navigateAfterRefresh;

        public string? EditorName { get; set; }
        public string? AlternateEditorName { get; set; }
        public string? SearchPanelName { get; set; }

        public DocumentSearchViewModel? Search
        {
            get => (DocumentSearchViewModel?)GetValue(SearchProperty);
            set => SetValue(SearchProperty, value);
        }

        public TextBoxBase? Editor
        {
            get => (TextBoxBase?)GetValue(EditorProperty) ?? FindEditor(EditorName);
            set => SetValue(EditorProperty, value);
        }

        public TextBoxBase? AlternateEditor
        {
            get => (TextBoxBase?)GetValue(AlternateEditorProperty) ?? FindEditor(AlternateEditorName);
            set => SetValue(AlternateEditorProperty, value);
        }

        public TextBox? SearchBox
        {
            get => (TextBox?)GetValue(SearchBoxProperty) ??
                (FindControl(SearchPanelName) as DocumentSearchBar)?.QueryBox;
            set => SetValue(SearchBoxProperty, value);
        }

        // Behaviors are outside the visual tree, so ElementName bindings do not
        // inherit the view's XAML namescope. Resolve controls in the host at load.
        private object? FindControl(string? name) => string.IsNullOrEmpty(name) ? null :
            AssociatedObject?.FindName(name);

        private TextBoxBase? FindEditor(string? name) => FindControl(name) switch
        {
            TextBoxBase editor => editor,
            ContentControl host => host.Content as TextBoxBase,
            _ => null
        };

        private static DependencyProperty Register(string name, Type type) =>
            DependencyProperty.Register(name, type, typeof(DocumentSearchBehavior),
                new PropertyMetadata(null, (target, _) =>
                {
                    var behavior = (DocumentSearchBehavior)target;
                    behavior.Stop();
                    if(behavior.AssociatedObject?.IsLoaded == true)
                        behavior.Start();
                }));

        protected override void OnAttached()
        {
            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Unloaded += OnUnloaded;
            AssociatedObject.IsVisibleChanged += OnHostVisibilityChanged;
            AssociatedObject.PreviewKeyDown += OnKeyDown;
            refreshTimer.Tick += OnRefresh;
            if(AssociatedObject.IsLoaded)
                Start();
        }

        protected override void OnDetaching()
        {
            Stop();
            refreshTimer.Tick -= OnRefresh;
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Unloaded -= OnUnloaded;
            AssociatedObject.IsVisibleChanged -= OnHostVisibilityChanged;
            AssociatedObject.PreviewKeyDown -= OnKeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs args) => Start();
        private void OnUnloaded(object sender, RoutedEventArgs args) => Stop();

        private void Start()
        {
            if(subscribedSearch is not null || Search is null)
                return;
            subscribedSearch = Search;
            subscribedSearch.PropertyChanged += OnSearchChanged;
            subscribedSearch.FocusRequested += OnFocusRequested;
            foreach(var editor in new[] { Editor, AlternateEditor }.OfType<TextBoxBase>().Distinct())
            {
                subscribedEditors.Add(editor);
                editor.TextChanged += OnTextChanged;
                editor.IsVisibleChanged += OnEditorVisibilityChanged;
                editor.SizeChanged += OnEditorSizeChanged;
                editor.AddHandler(ScrollViewer.ScrollChangedEvent,
                    new ScrollChangedEventHandler(OnScrollChanged));
            }
            QueueRefresh(true);
        }

        private void Stop()
        {
            refreshTimer.Stop();
            if(subscribedSearch is not null)
            {
                subscribedSearch.PropertyChanged -= OnSearchChanged;
                subscribedSearch.FocusRequested -= OnFocusRequested;
                subscribedSearch = null;
            }
            foreach(var editor in subscribedEditors)
            {
                editor.TextChanged -= OnTextChanged;
                editor.IsVisibleChanged -= OnEditorVisibilityChanged;
                editor.SizeChanged -= OnEditorSizeChanged;
                editor.RemoveHandler(ScrollViewer.ScrollChangedEvent,
                    new ScrollChangedEventHandler(OnScrollChanged));
            }
            subscribedEditors.Clear();
            ClearResults();
        }

        private void OnSearchChanged(object? sender, PropertyChangedEventArgs args)
        {
            if(updatingResults)
                return;
            switch(args.PropertyName)
            {
                case nameof(DocumentSearchViewModel.Query):
                case nameof(DocumentSearchViewModel.MatchCase):
                    QueueRefresh(true);
                    break;
                case nameof(DocumentSearchViewModel.IsOpen):
                    QueueRefresh(true);
                    if(Search?.IsOpen == false)
                        GetActiveEditor()?.Focus();
                    break;
                case nameof(DocumentSearchViewModel.CurrentIndex):
                    Navigate();
                    break;
            }
        }

        private void OnFocusRequested(object? sender, EventArgs args)
        {
            AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if(AssociatedObject.IsLoaded && Search?.IsOpen == true)
                {
                    SearchBox?.Focus();
                    SearchBox?.SelectAll();
                }
            }));
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
        {
            if(Search is null)
                return;
            var modifiers = Keyboard.Modifiers;
            if(args.Key == Key.F && modifiers == ModifierKeys.Control)
                Search.Open.Execute(null);
            else if(args.Key == Key.Escape && Search.IsOpen)
                Search.Close.Execute(null);
            else if(Search.IsOpen &&
                    ((args.Key == Key.F3 && (modifiers & ~ModifierKeys.Shift) == 0) ||
                     (args.Key == Key.Enter && SearchBox?.IsKeyboardFocusWithin == true)))
            {
                // Enter must include the last keystroke even before the debounce expires.
                if(refreshTimer.IsEnabled)
                {
                    RefreshNow();
                    if(!modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        args.Handled = true;
                        return;
                    }
                }
                (modifiers.HasFlag(ModifierKeys.Shift) ? Search.Previous : Search.Next).Execute(null);
            }
            else
                return;
            args.Handled = true;
        }

        private TextBoxBase? GetActiveEditor() =>
            subscribedEditors.FirstOrDefault(editor => editor.IsVisible);

        private void OnHostVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) =>
            QueueRefresh(false);
        private void OnEditorVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) =>
            QueueRefresh(false);
        private void OnTextChanged(object sender, TextChangedEventArgs args) => QueueRefresh(false);
        private void OnEditorSizeChanged(object sender, SizeChangedEventArgs args) => adorner?.InvalidateVisual();
        private void OnScrollChanged(object sender, ScrollChangedEventArgs args) => adorner?.InvalidateVisual();

        private void QueueRefresh(bool navigate)
        {
            refreshTimer.Stop();
            ClearResults();
            navigateAfterRefresh |= navigate;
            if(subscribedSearch?.IsOpen == true && AssociatedObject.IsVisible)
                refreshTimer.Start();
        }

        private void OnRefresh(object? sender, EventArgs args) => RefreshNow();

        internal void RefreshNow()
        {
            refreshTimer.Stop();
            ClearResults();
            if(Search?.IsOpen != true || !AssociatedObject.IsVisible)
                return;
            activeEditor = GetActiveEditor();
            if(activeEditor is RichTextBox rich)
                richMatches = DocumentTextSearch.Find(rich.Document, Search.Query, Search.MatchCase);
            else if(activeEditor is TextBox text)
                textMatches = DocumentTextSearch.Find(text.Text, Search.Query, Search.MatchCase);

            updatingResults = true;
            try
            {
                Search.SetResultCount(richMatches.Count + textMatches.Count);
            }
            finally
            {
                updatingResults = false;
            }
            if(activeEditor is not null && Search.ResultCount > 0)
            {
                adornerLayer = AdornerLayer.GetAdornerLayer(activeEditor);
                if(adornerLayer is not null)
                {
                    adorner = new DocumentSearchAdorner(activeEditor, richMatches, textMatches);
                    adornerLayer.Add(adorner);
                    adorner.CurrentIndex = Search.CurrentIndex;
                }
                if(navigateAfterRefresh)
                    Navigate();
            }
            navigateAfterRefresh = false;
        }

        private void ClearResults()
        {
            if(adorner is not null)
                adornerLayer?.Remove(adorner);
            adorner = null;
            adornerLayer = null;
            richMatches = [];
            textMatches = [];
            activeEditor = null;
            updatingResults = true;
            try { Search?.SetResultCount(0); }
            finally { updatingResults = false; }
        }

        private void Navigate()
        {
            int index = Search?.CurrentIndex ?? -1;
            if(index < 0 || activeEditor is null)
                return;
            if(activeEditor is RichTextBox rich && index < richMatches.Count)
            {
                var range = richMatches[index];
                rich.Selection.Select(range.Start, range.End);
                rich.UpdateLayout();
                if(range.Start.HasValidLayout)
                {
                    var bounds = range.Start.GetCharacterRect(LogicalDirection.Forward);
                    if(bounds.Top < 0 || bounds.Bottom > rich.ViewportHeight)
                        rich.ScrollToVerticalOffset(rich.VerticalOffset + bounds.Top - rich.ViewportHeight / 2);
                    if(bounds.Left < 0 || bounds.Right > rich.ViewportWidth)
                        rich.ScrollToHorizontalOffset(rich.HorizontalOffset + bounds.Left - rich.ViewportWidth / 2);
                }
            }
            else if(activeEditor is TextBox text && index < textMatches.Count)
            {
                var match = textMatches[index];
                text.Select(match.Start, match.Length);
                text.ScrollToLine(text.GetLineIndexFromCharacterIndex(match.Start));
                var bounds = text.GetRectFromCharacterIndex(match.Start);
                if(!bounds.IsEmpty && (bounds.Left < 0 || bounds.Right > text.ViewportWidth))
                    text.ScrollToHorizontalOffset(text.HorizontalOffset + bounds.Left - text.ViewportWidth / 2);
            }
            if(adorner is not null)
            {
                adorner.CurrentIndex = index;
                adorner.InvalidateVisual();
            }
        }
    }
}

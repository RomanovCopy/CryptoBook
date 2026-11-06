using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using RichTextBox = System.Windows.Controls.RichTextBox;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;

namespace CryptoBook.Adorners;

/// <summary>A temporary viewport marker; never changes document content or formatting.</summary>
internal sealed class BookmarkNavigationAdorner : Adorner
{
    private static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached(
        "Current", typeof(BookmarkNavigationAdorner), typeof(BookmarkNavigationAdorner));
    private readonly RichTextBox editor;
    private readonly FlowDocument document;
    private readonly TextPointer position;
    private readonly AdornerLayer layer;
    private readonly DispatcherTimer timer;

    private BookmarkNavigationAdorner(RichTextBox editor, TextPointer position, AdornerLayer layer) : base(editor)
    {
        this.editor = editor;
        document = editor.Document;
        this.position = position;
        this.layer = layer;
        IsHitTestVisible = false;
        timer = new DispatcherTimer(DispatcherPriority.Background, editor.Dispatcher) {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        timer.Tick += OnExpired;
        editor.Unloaded += OnUnloaded;
        editor.TextChanged += OnTextChanged;
        editor.SelectionChanged += OnSelectionChanged;
        editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
        timer.Start();
    }

    public static void Show(RichTextBox editor, TextPointer position)
    {
        Clear(editor);
        if(AdornerLayer.GetAdornerLayer(editor) is not { } layer)
            return;
        var marker = new BookmarkNavigationAdorner(editor, position, layer);
        editor.SetValue(CurrentProperty, marker);
        layer.Add(marker);
    }

    public static void Clear(RichTextBox editor) =>
        (editor.GetValue(CurrentProperty) as BookmarkNavigationAdorner)?.Detach();

    private void Detach()
    {
        timer.Stop();
        timer.Tick -= OnExpired;
        editor.Unloaded -= OnUnloaded;
        editor.TextChanged -= OnTextChanged;
        editor.SelectionChanged -= OnSelectionChanged;
        editor.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
        layer.Remove(this);
        editor.ClearValue(CurrentProperty);
    }

    internal Rect GetVisibleBounds()
    {
        if(!ReferenceEquals(editor.Document, document) || !position.HasValidLayout)
            return Rect.Empty;
        var caret = position.GetCharacterRect(LogicalDirection.Forward);
        if(caret.IsEmpty || !double.IsFinite(caret.Top) || editor.ViewportWidth <= 4)
            return Rect.Empty;
        var bounds = new Rect(editor.BorderThickness.Left + 2, caret.Top - 3,
            editor.ViewportWidth - 4, caret.Height + 6);
        bounds.Intersect(new Rect(editor.RenderSize));
        return bounds;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = GetVisibleBounds();
        if(bounds.IsEmpty)
            return;
        Color color = (editor.TryFindResource("CurrentAccent") as SolidColorBrush)?.Color ?? Colors.DarkOrange;
        Brush stroke = new SolidColorBrush(color);
        Brush fill = new SolidColorBrush(color) { Opacity = 0.24 };
        drawingContext.PushClip(new RectangleGeometry(new Rect(editor.RenderSize)));
        drawingContext.DrawRoundedRectangle(fill, new Pen(stroke, 1.5), bounds, 3, 3);
        drawingContext.DrawRectangle(stroke, null, new Rect(bounds.Left, bounds.Top, 4, bounds.Height));
        drawingContext.Pop();
    }

    private void OnExpired(object? sender, EventArgs args) => Detach();
    private void OnUnloaded(object sender, RoutedEventArgs args) => Detach();
    private void OnTextChanged(object sender, TextChangedEventArgs args) => Detach();
    private void OnSelectionChanged(object sender, RoutedEventArgs args)
    {
        if(!ReferenceEquals(editor.Document, document) || editor.CaretPosition.CompareTo(position) != 0)
            Detach();
    }
    private void OnScrollChanged(object sender, ScrollChangedEventArgs args) => InvalidateVisual();
}

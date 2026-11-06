using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CryptoBook.Adorners;

/// <summary>Временная подсветка строки в предпросмотре без изменения его документа.</summary>
internal sealed class BookmarkPreviewNavigationAdorner : Adorner
{
    private static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached(
        "Current", typeof(BookmarkPreviewNavigationAdorner), typeof(BookmarkPreviewNavigationAdorner));
    private static readonly DependencyPropertyDescriptor DocumentDescriptor =
        DependencyPropertyDescriptor.FromProperty(DocumentViewerBase.DocumentProperty, typeof(FlowDocumentPageViewer));
    private static readonly DependencyPropertyDescriptor PageDescriptor =
        DependencyPropertyDescriptor.FromProperty(DocumentViewerBase.MasterPageNumberProperty, typeof(FlowDocumentPageViewer));
    private readonly FlowDocumentPageViewer viewer;
    private readonly UIElement scope;
    private readonly FlowDocument document;
    private readonly TextPointer position;
    private readonly AdornerLayer layer;
    private readonly DispatcherTimer timer;
    private Rect lastBounds = Rect.Empty;
    private bool detached;

    private BookmarkPreviewNavigationAdorner(FlowDocumentPageViewer viewer, UIElement scope,
        TextPointer position, AdornerLayer layer) : base(scope)
    {
        this.viewer = viewer;
        this.scope = scope;
        document = (FlowDocument)viewer.Document;
        this.position = position;
        this.layer = layer;
        IsHitTestVisible = false;
        timer = new DispatcherTimer(DispatcherPriority.Background, viewer.Dispatcher) {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        timer.Tick += OnExpired;
        viewer.Unloaded += OnUnloaded;
        viewer.IsVisibleChanged += OnVisibilityChanged;
        viewer.LayoutUpdated += OnLayoutUpdated;
        DocumentDescriptor.AddValueChanged(viewer, OnViewChanged);
        PageDescriptor.AddValueChanged(viewer, OnViewChanged);
        timer.Start();
    }

    public static void Show(FlowDocumentPageViewer viewer, TextPointer position)
    {
        Clear(viewer);
        if(!viewer.IsVisible || viewer.Document is not FlowDocument || !position.HasValidLayout)
            return;
        var pageView = viewer.PageViews.FirstOrDefault();
        if(pageView == null || AdornerLayer.GetAdornerLayer(pageView) is not { } layer ||
           VisualTreeHelper.GetParent(layer) is not AdornerDecorator { Child: { } scope })
            return;

        // WPF returns paged TextPointer rectangles in the coordinate system of
        // its template's AdornerDecorator.Child, including page zoom and offsets.
        var marker = new BookmarkPreviewNavigationAdorner(viewer, scope, position, layer);
        viewer.SetValue(CurrentProperty, marker);
        layer.Add(marker);
    }

    public static void Clear(FlowDocumentPageViewer viewer) =>
        (viewer.GetValue(CurrentProperty) as BookmarkPreviewNavigationAdorner)?.Detach();

    private void Detach()
    {
        if(detached)
            return;
        detached = true;
        timer.Stop();
        timer.Tick -= OnExpired;
        viewer.Unloaded -= OnUnloaded;
        viewer.IsVisibleChanged -= OnVisibilityChanged;
        viewer.LayoutUpdated -= OnLayoutUpdated;
        DocumentDescriptor.RemoveValueChanged(viewer, OnViewChanged);
        PageDescriptor.RemoveValueChanged(viewer, OnViewChanged);
        layer.Remove(this);
        viewer.ClearValue(CurrentProperty);
    }

    internal Rect GetVisibleBounds()
    {
        if(!viewer.IsVisible || !ReferenceEquals(viewer.Document, document) || !position.HasValidLayout)
            return Rect.Empty;
        var caret = position.GetCharacterRect(LogicalDirection.Forward);
        if(caret.IsEmpty || !double.IsFinite(caret.Top) || !double.IsFinite(caret.Height) || caret.Height <= 0)
            return Rect.Empty;

        foreach(var pageView in viewer.PageViews)
        {
            var page = pageView.DocumentPage;
            if(page?.Visual is not { } visual || !scope.IsAncestorOf(visual))
                continue;
            var transform = visual.TransformToAncestor(scope);
            var pageBounds = transform.TransformBounds(new Rect(page.Size));
            if(!pageBounds.Contains(new Point(caret.Left, caret.Top + caret.Height / 2)))
                continue;
            var content = transform.TransformBounds(page.ContentBox);
            if(content.IsEmpty || content.Width <= 0)
                return Rect.Empty;
            var bounds = new Rect(content.Left - 6, caret.Top - 3, content.Width + 12, caret.Height + 6);
            bounds.Intersect(pageBounds);
            bounds.Intersect(new Rect(scope.RenderSize));
            return bounds;
        }
        return Rect.Empty;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = GetVisibleBounds();
        if(bounds.IsEmpty)
            return;
        Color color = (viewer.TryFindResource("CurrentAccent") as SolidColorBrush)?.Color ?? Colors.DarkOrange;
        Brush stroke = new SolidColorBrush(color);
        Brush fill = new SolidColorBrush(color) { Opacity = 0.24 };
        drawingContext.PushClip(new RectangleGeometry(new Rect(scope.RenderSize)));
        drawingContext.DrawRoundedRectangle(fill, new Pen(stroke, 1.5), bounds, 3, 3);
        drawingContext.DrawRectangle(stroke, null, new Rect(bounds.Left, bounds.Top, 4, bounds.Height));
        drawingContext.Pop();
    }

    private void OnExpired(object? sender, EventArgs args) => Detach();
    private void OnUnloaded(object sender, RoutedEventArgs args) => Detach();
    private void OnViewChanged(object? sender, EventArgs args) => Detach();
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if(args.NewValue is false)
            Detach();
    }
    private void OnLayoutUpdated(object? sender, EventArgs args)
    {
        var bounds = GetVisibleBounds();
        if(bounds == lastBounds)
            return;
        lastBounds = bounds;
        InvalidateVisual();
    }
}

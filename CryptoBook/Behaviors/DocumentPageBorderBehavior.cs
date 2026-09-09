using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RichTextBox = System.Windows.Controls.RichTextBox;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;

namespace CryptoBook.Behaviors
{
    /// <summary>Fits the continuous page to the available width with fixed outer gutters and outlines its three edges.</summary>
    public static class DocumentPageBorderBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool),
                typeof(DocumentPageBorderBehavior), new PropertyMetadata(false, OnEnabledChanged));

        private static readonly DependencyProperty AdornerProperty =
            DependencyProperty.RegisterAttached("Adorner", typeof(PageBorderAdorner),
                typeof(DocumentPageBorderBehavior));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        {
            if(element is not RichTextBox editor)
                return;
            if(args.NewValue is true)
            {
                editor.Loaded += Attach;
                editor.Unloaded += Detach;
                if(editor.IsLoaded)
                    Attach(editor, new RoutedEventArgs());
            }
            else
            {
                editor.Loaded -= Attach;
                editor.Unloaded -= Detach;
                Detach(editor, new RoutedEventArgs());
            }
        }

        private static void Attach(object sender, RoutedEventArgs args)
        {
            var editor = (RichTextBox)sender;
            if(editor.GetValue(AdornerProperty) is not null ||
               AdornerLayer.GetAdornerLayer(editor) is not { } layer)
                return;
            var adorner = new PageBorderAdorner(editor);
            editor.SetValue(AdornerProperty, adorner);
            layer.Add(adorner);
            editor.LayoutUpdated += adorner.Update;
            adorner.Update(null, EventArgs.Empty);
        }

        private static void Detach(object sender, RoutedEventArgs args)
        {
            var editor = (RichTextBox)sender;
            if(editor.GetValue(AdornerProperty) is not PageBorderAdorner adorner)
                return;
            editor.LayoutUpdated -= adorner.Update;
            adorner.RestoreLayout();
            (VisualTreeHelper.GetParent(adorner) as AdornerLayer)?.Remove(adorner);
            editor.ClearValue(AdornerProperty);
        }

        private sealed class PageBorderAdorner(RichTextBox editor) : Adorner(editor)
        {
            private readonly Thickness originalMargin = editor.Margin;
            private readonly Transform originalTransform = editor.LayoutTransform;
            private ScaleTransform pageScale = new();
            private readonly Dictionary<ScrollBar, Transform> scrollbarTransforms = new();
            private Rect viewport;
            private double pageWidth;
            private double horizontalOffset;
            private double verticalOffset;

            public void Update(object? sender, EventArgs args)
            {
                IsHitTestVisible = false;
                var presenter = FindPresenter(editor);
                if(presenter is null)
                    return;
                if(UpdatePageLayout(presenter))
                    return;
                var bounds = new Rect(presenter.TranslatePoint(new Point(), editor), presenter.RenderSize);
                if(bounds == viewport && pageWidth == editor.Document.PageWidth &&
                   horizontalOffset == editor.HorizontalOffset && verticalOffset == editor.VerticalOffset)
                    return;
                viewport = bounds;
                pageWidth = editor.Document.PageWidth;
                horizontalOffset = editor.HorizontalOffset;
                verticalOffset = editor.VerticalOffset;
                InvalidateVisual();
            }

            public void RestoreLayout()
            {
                editor.SetCurrentValue(FrameworkElement.MarginProperty, originalMargin);
                editor.SetCurrentValue(FrameworkElement.LayoutTransformProperty, originalTransform);
                foreach(var (scrollbar, transform) in scrollbarTransforms)
                    scrollbar.SetCurrentValue(FrameworkElement.LayoutTransformProperty, transform);
                scrollbarTransforms.Clear();
            }

            private bool UpdatePageLayout(ScrollContentPresenter presenter)
            {
                const double gutter = 24;
                if(editor.Parent is not FrameworkElement host || host.ActualWidth <= 2 * gutter ||
                   !double.IsFinite(editor.Document.PageWidth) || editor.Document.PageWidth <= 0)
                    return false;

                // Scale the view, keeping the paper size, line wrapping and saved document unchanged.
                double scale = (host.ActualWidth - 2 * gutter) / editor.Document.PageWidth;
                bool changed = false;
                if(Math.Abs(pageScale.ScaleX - scale) > 0.0000001 || editor.LayoutTransform != pageScale)
                {
                    pageScale = new ScaleTransform(scale, scale);
                    pageScale.Freeze();
                    editor.SetCurrentValue(FrameworkElement.LayoutTransformProperty, pageScale);
                    InvalidateVisual();
                    changed = true;
                }
                if(presenter.TemplatedParent is ScrollViewer scrollViewer)
                {
                    // Keep scrollbar controls at their normal screen size, including at large zoom.
                    foreach(var scrollbar in FindScrollbars(scrollViewer))
                    {
                        if(scrollbar.LayoutTransform is ScaleTransform current &&
                           Math.Abs(current.ScaleX - 1 / scale) < 0.0000001)
                            continue;
                        scrollbarTransforms.TryAdd(scrollbar, scrollbar.LayoutTransform);
                        var inverseScale = new ScaleTransform(1 / scale, 1 / scale);
                        inverseScale.Freeze();
                        scrollbar.SetCurrentValue(FrameworkElement.LayoutTransformProperty, inverseScale);
                        changed = true;
                    }
                }
                double rightGutter = gutter;
                if(presenter.TemplatedParent is ScrollViewer { ComputedVerticalScrollBarVisibility: Visibility.Visible })
                {
                    // The scrollbar occupies the right gutter in screen coordinates.
                    rightGutter -= Math.Max(0, editor.ActualWidth - presenter.ActualWidth) * scale;
                }
                var margin = new Thickness(gutter, gutter, rightGutter, 0);
                if(editor.Margin != margin)
                {
                    editor.SetCurrentValue(FrameworkElement.MarginProperty, margin);
                    changed = true;
                }
                return changed;
            }

            protected override void OnRender(DrawingContext drawing)
            {
                if(viewport.IsEmpty || !double.IsFinite(pageWidth))
                    return;
                drawing.PushClip(new RectangleGeometry(viewport));
                double strokeWidth = 1 / pageScale.ScaleX;
                double left = viewport.Left - horizontalOffset + strokeWidth / 2;
                double right = left + pageWidth - strokeWidth;
                double top = viewport.Top - verticalOffset + strokeWidth / 2;
                var pen = new Pen(editor.BorderBrush ?? Brushes.Gray, strokeWidth);
                drawing.DrawLine(pen, new Point(left, top), new Point(right, top));
                drawing.DrawLine(pen, new Point(left, top), new Point(left, viewport.Bottom));
                drawing.DrawLine(pen, new Point(right, top), new Point(right, viewport.Bottom));
                drawing.Pop();
            }

            private static ScrollContentPresenter? FindPresenter(DependencyObject parent)
            {
                for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                    if(child is ScrollContentPresenter presenter)
                        return presenter;
                    if(FindPresenter(child) is { } nested)
                        return nested;
                }
                return null;
            }

            private static IEnumerable<ScrollBar> FindScrollbars(DependencyObject parent)
            {
                for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                    if(child is ScrollBar scrollbar)
                        yield return scrollbar;
                    else if(child is not ScrollContentPresenter)
                        foreach(var nested in FindScrollbars(child))
                            yield return nested;
                }
            }
        }
    }
}

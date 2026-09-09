using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RichTextBox = System.Windows.Controls.RichTextBox;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;

namespace CryptoBook.Behaviors
{
    /// <summary>Centers the continuous page with equal outer gutters and outlines its three edges.</summary>
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
            adorner.RestoreMargin();
            (VisualTreeHelper.GetParent(adorner) as AdornerLayer)?.Remove(adorner);
            editor.ClearValue(AdornerProperty);
        }

        private sealed class PageBorderAdorner(RichTextBox editor) : Adorner(editor)
        {
            private readonly Thickness originalMargin = editor.Margin;
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
                if(UpdatePageMargin(presenter))
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

            public void RestoreMargin() => editor.SetCurrentValue(FrameworkElement.MarginProperty, originalMargin);

            private bool UpdatePageMargin(ScrollContentPresenter presenter)
            {
                if(editor.Parent is not FrameworkElement host || host.ActualWidth <= 0 ||
                   !double.IsFinite(editor.Document.PageWidth))
                    return false;

                const double minimumGutter = 24;
                double gutter = Math.Max(minimumGutter, (host.ActualWidth - editor.Document.PageWidth) / 2);
                double rightGutter = gutter;
                if(editor.Document.PageWidth + 2 * minimumGutter <= host.ActualWidth &&
                   presenter.TemplatedParent is ScrollViewer { ComputedVerticalScrollBarVisibility: Visibility.Visible })
                {
                    // The scrollbar occupies the right gutter; it must not push the paper off center.
                    rightGutter -= Math.Max(0, editor.ActualWidth - presenter.ActualWidth);
                }
                var margin = new Thickness(gutter, gutter, rightGutter, 0);
                if(editor.Margin == margin)
                    return false;
                editor.SetCurrentValue(FrameworkElement.MarginProperty, margin);
                return true;
            }

            protected override void OnRender(DrawingContext drawing)
            {
                if(viewport.IsEmpty || !double.IsFinite(pageWidth))
                    return;
                drawing.PushClip(new RectangleGeometry(viewport));
                double left = viewport.Left - horizontalOffset + 0.5;
                double right = left + pageWidth - 1;
                double top = viewport.Top - verticalOffset + 0.5;
                var pen = new Pen(editor.BorderBrush ?? Brushes.Gray, 1);
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
        }
    }
}

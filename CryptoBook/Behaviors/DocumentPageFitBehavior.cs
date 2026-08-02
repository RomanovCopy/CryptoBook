using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

using WpfSize = System.Windows.Size;

namespace CryptoBook.Behaviors
{
    /// <summary>
    /// Подбирает масштаб так, чтобы страница целиком помещалась в область просмотра.
    /// </summary>
    public static class DocumentPageFitBehavior
    {
        private const double HorizontalChrome = 64;
        private const double VerticalChrome = 96;

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(DocumentPageFitBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        private static readonly DependencyProperty StateProperty =
            DependencyProperty.RegisterAttached(
                "State",
                typeof(FitState),
                typeof(DocumentPageFitBehavior),
                new PropertyMetadata(null));

        public static void SetIsEnabled(
            DependencyObject element,
            bool value) =>
            element.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject element) =>
            (bool)element.GetValue(IsEnabledProperty);

        public static double CalculateZoom(
            WpfSize viewport,
            WpfSize page,
            double minimumZoom,
            double maximumZoom)
        {
            if(!double.IsFinite(page.Width) ||
               !double.IsFinite(page.Height) ||
               viewport.Width <= HorizontalChrome ||
               viewport.Height <= VerticalChrome ||
               page.Width <= 0 ||
               page.Height <= 0)
            {
                return Math.Clamp(
                    100,
                    minimumZoom,
                    maximumZoom);
            }

            // Запас учитывает рамки и встроенные панели FlowDocumentPageViewer,
            // которые не входят в размер страницы пагинатора.
            double horizontalScale =
                (viewport.Width - HorizontalChrome) / page.Width;
            double verticalScale =
                (viewport.Height - VerticalChrome) / page.Height;
            double zoom = Math.Floor(
                Math.Min(horizontalScale, verticalScale) * 100);
            return Math.Clamp(
                zoom,
                minimumZoom,
                maximumZoom);
        }

        private static void OnIsEnabledChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if(dependencyObject is not FlowDocumentPageViewer viewer)
                return;

            FitState state =
                (FitState?)viewer.GetValue(StateProperty) ??
                new FitState(viewer);
            viewer.SetValue(StateProperty, state);
            state.IsEnabled = args.NewValue is true;

            if(!state.IsEnabled)
                viewer.Zoom = Math.Clamp(100, viewer.MinZoom, viewer.MaxZoom);
        }

        private sealed class FitState
        {
            private readonly FlowDocumentPageViewer viewer;
            private bool isEnabled;

            public FitState(FlowDocumentPageViewer viewer)
            {
                this.viewer = viewer;
                viewer.Loaded += OnLayoutChanged;
                viewer.SizeChanged += OnLayoutChanged;
                DependencyPropertyDescriptor.FromProperty(
                    FlowDocumentPageViewer.DocumentProperty,
                    typeof(FlowDocumentPageViewer)).AddValueChanged(
                        viewer,
                        OnDocumentChanged);
            }

            public bool IsEnabled
            {
                get => isEnabled;
                set
                {
                    if(isEnabled == value)
                        return;

                    isEnabled = value;
                    if(value)
                        ScheduleFit();
                }
            }

            private void OnLayoutChanged(
                object? sender,
                RoutedEventArgs args)
            {
                if(IsEnabled)
                    ScheduleFit();
            }

            private void OnDocumentChanged(
                object? sender,
                EventArgs args)
            {
                if(IsEnabled)
                    ScheduleFit();
            }

            private void ScheduleFit()
            {
                // Размер страницы стабилизируется только после прохода компоновки.
                viewer.Dispatcher.BeginInvoke(
                    ApplyFit,
                    DispatcherPriority.Loaded);
            }

            private void ApplyFit()
            {
                if(!IsEnabled ||
                   viewer.Document is not FlowDocument document ||
                   viewer.ActualWidth <= 0 ||
                   viewer.ActualHeight <= 0)
                {
                    return;
                }

                DocumentPaginator paginator =
                    ((IDocumentPaginatorSource)document).DocumentPaginator;
                WpfSize pageSize = paginator.PageSize;
                viewer.Zoom = CalculateZoom(
                    new WpfSize(
                        viewer.ActualWidth,
                        viewer.ActualHeight),
                    pageSize,
                    viewer.MinZoom,
                    viewer.MaxZoom);
            }
        }
    }
}

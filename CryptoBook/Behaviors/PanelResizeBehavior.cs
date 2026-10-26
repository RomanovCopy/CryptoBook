using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CryptoBook.Behaviors;

public sealed class PanelResizeBehavior : Behavior<FrameworkElement>
{
    private Grid? hostGrid;

    public static readonly DependencyProperty IsResizeHandleProperty = DependencyProperty.RegisterAttached(
        "IsResizeHandle", typeof(bool), typeof(PanelResizeBehavior), new PropertyMetadata(false));
    public static readonly DependencyProperty PreferredWidthProperty = DependencyProperty.Register(
        nameof(PreferredWidth), typeof(double), typeof(PanelResizeBehavior), new PropertyMetadata(390d));
    public static readonly DependencyProperty MinimumPanelWidthProperty = DependencyProperty.Register(
        nameof(MinimumPanelWidth), typeof(double), typeof(PanelResizeBehavior), new PropertyMetadata(280d));
    public static readonly DependencyProperty MinimumRemainingWidthProperty = DependencyProperty.Register(
        nameof(MinimumRemainingWidth), typeof(double), typeof(PanelResizeBehavior), new PropertyMetadata(220d));

    public static bool GetIsResizeHandle(DependencyObject element) => (bool)element.GetValue(IsResizeHandleProperty);
    public static void SetIsResizeHandle(DependencyObject element, bool value) => element.SetValue(IsResizeHandleProperty, value);
    public double PreferredWidth
    {
        get => (double)GetValue(PreferredWidthProperty);
        set => SetValue(PreferredWidthProperty, value);
    }
    public double MinimumPanelWidth
    {
        get => (double)GetValue(MinimumPanelWidthProperty);
        set => SetValue(MinimumPanelWidthProperty, value);
    }
    public double MinimumRemainingWidth
    {
        get => (double)GetValue(MinimumRemainingWidthProperty);
        set => SetValue(MinimumRemainingWidthProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.Unloaded += OnUnloaded;
        AssociatedObject.IsVisibleChanged += OnVisibilityChanged;
        AssociatedObject.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnDragDelta));
        if(AssociatedObject.IsLoaded) AttachHost();
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.Unloaded -= OnUnloaded;
        AssociatedObject.IsVisibleChanged -= OnVisibilityChanged;
        AssociatedObject.RemoveHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnDragDelta));
        DetachHost();
        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => AttachHost();
    private void OnUnloaded(object sender, RoutedEventArgs e) => DetachHost();
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if(AssociatedObject.IsVisible) AttachHost();
    }
    private void AttachHost()
    {
        DetachHost();
        hostGrid = VisualTreeHelper.GetParent(AssociatedObject) as Grid;
        if(hostGrid is not null) hostGrid.SizeChanged += OnHostSizeChanged;
        FitToHost();
    }
    private void DetachHost()
    {
        if(hostGrid is not null) hostGrid.SizeChanged -= OnHostSizeChanged;
        hostGrid = null;
    }
    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e) => FitToHost();

    private (double Min, double Max) WidthLimits()
    {
        if(hostGrid is null || hostGrid.ActualWidth <= 0) return (MinimumPanelWidth, double.MaxValue);
        double available = hostGrid.ActualWidth;
        int panelColumn = Grid.GetColumn(AssociatedObject);
        for(int i = 0; i < hostGrid.ColumnDefinitions.Count; i++)
            if(i != panelColumn && !hostGrid.ColumnDefinitions[i].Width.IsStar)
                available -= hostGrid.ColumnDefinitions[i].ActualWidth;
        available = Math.Max(0, available);
        double minimum = Math.Min(MinimumPanelWidth, available);
        return (minimum, Math.Max(minimum, available - MinimumRemainingWidth));
    }

    private void FitToHost()
    {
        var (minimum, maximum) = WidthLimits();
        AssociatedObject.SetCurrentValue(FrameworkElement.WidthProperty, Math.Clamp(PreferredWidth, minimum, maximum));
    }
    private void OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if(e.OriginalSource is not Thumb handle || !GetIsResizeHandle(handle)) return;
        var (minimum, maximum) = WidthLimits();
        SetCurrentValue(PreferredWidthProperty, Math.Clamp(AssociatedObject.ActualWidth - e.HorizontalChange, minimum, maximum));
        FitToHost();
        e.Handled = true;
    }
}

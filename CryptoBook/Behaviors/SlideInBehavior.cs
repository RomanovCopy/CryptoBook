using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CryptoBook.Behaviors;

public sealed class SlideInBehavior : Behavior<FrameworkElement>
{
    private DispatcherOperation? pendingAnimation;
    private TranslateTransform? animatedTransform;

    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration), typeof(TimeSpan), typeof(SlideInBehavior), new PropertyMetadata(TimeSpan.FromMilliseconds(180)));
    public TimeSpan Duration
    {
        get => (TimeSpan)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.Unloaded += OnUnloaded;
        AssociatedObject.IsVisibleChanged += OnVisibilityChanged;
        if(AssociatedObject.IsLoaded && AssociatedObject.IsVisible) QueueAnimation();
    }
    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.Unloaded -= OnUnloaded;
        AssociatedObject.IsVisibleChanged -= OnVisibilityChanged;
        StopAnimation();
        base.OnDetaching();
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if(AssociatedObject.IsVisible) QueueAnimation();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e) => StopAnimation();
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if(AssociatedObject.IsVisible) QueueAnimation();
        else StopAnimation();
    }
    private void QueueAnimation()
    {
        StopAnimation();
        pendingAnimation = AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            pendingAnimation = null;
            if(!AssociatedObject.IsLoaded || !AssociatedObject.IsVisible ||
               AssociatedObject.RenderTransform is not TranslateTransform transform) return;
            if(transform.IsFrozen)
            {
                transform = transform.Clone();
                AssociatedObject.SetCurrentValue(UIElement.RenderTransformProperty, transform);
            }
            animatedTransform = transform;
            transform.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(AssociatedObject.ActualWidth, 0, new Duration(Duration)) { FillBehavior = FillBehavior.Stop });
        }));
    }
    private void StopAnimation()
    {
        pendingAnimation?.Abort();
        pendingAnimation = null;
        animatedTransform?.BeginAnimation(TranslateTransform.XProperty, null);
        animatedTransform = null;
    }
}

using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;

namespace CryptoBook.Behaviors;

public sealed class ScrollToEndBehavior : Behavior<ScrollViewer>
{
    private bool followContent = true;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.ScrollChanged += OnScrollChanged;
    }
    protected override void OnDetaching()
    {
        AssociatedObject.ScrollChanged -= OnScrollChanged;
        base.OnDetaching();
    }
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if(e.OriginalSource != AssociatedObject) return;
        if(e.ExtentHeightChange == 0)
            followContent = AssociatedObject.VerticalOffset >= AssociatedObject.ScrollableHeight - 2;
        if(followContent && e.ExtentHeightChange != 0) AssociatedObject.ScrollToEnd();
    }
}

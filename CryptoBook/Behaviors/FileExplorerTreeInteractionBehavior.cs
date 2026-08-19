using Microsoft.Xaml.Behaviors;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using WpfTreeView = System.Windows.Controls.TreeView;

namespace CryptoBook.Behaviors;

/// <summary>
/// Keeps FileExplorer tree navigation stable when a user double-clicks a row.
/// Expansion remains available through the dedicated expander and keyboard.
/// </summary>
public sealed class FileExplorerTreeInteractionBehavior: Behavior<WpfTreeView>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseLeftButtonDown +=
            PreviewMouseLeftButtonDown;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewMouseLeftButtonDown -=
            PreviewMouseLeftButtonDown;
        base.OnDetaching();
    }

    private void PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if(!ShouldSuppressHeaderDoubleClick(
            e.OriginalSource as DependencyObject,
            e.ChangedButton,
            e.ClickCount))
        {
            return;
        }

        // TreeViewItem toggles IsExpanded on the second header click. The
        // first click has already selected and navigated to the directory,
        // so that toggle makes the freshly displayed branch disappear.
        e.Handled = true;
    }

    internal static bool ShouldSuppressHeaderDoubleClick(
        DependencyObject? source,
        MouseButton changedButton,
        int clickCount) =>
        changedButton == MouseButton.Left &&
        clickCount == 2 &&
        FindAncestor<TreeViewItem>(source) is not null &&
        FindAncestor<ToggleButton>(source) is null;

    private static T? FindAncestor<T>(DependencyObject? current)
        where T: DependencyObject
    {
        while(current is not null)
        {
            if(current is T target)
                return target;

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}

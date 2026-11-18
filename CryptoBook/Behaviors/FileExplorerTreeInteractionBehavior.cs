using Microsoft.Xaml.Behaviors;

using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

using WpfTreeView = System.Windows.Controls.TreeView;

namespace CryptoBook.Behaviors;

/// <summary>
/// Keeps FileExplorer tree navigation stable when a user double-clicks a row.
/// Expansion remains available through the dedicated expander and keyboard.
/// </summary>
public sealed class FileExplorerTreeInteractionBehavior: Behavior<WpfTreeView>
{
    private bool adjustingArchiveVisibility;
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseLeftButtonDown +=
            PreviewMouseLeftButtonDown;
        AssociatedObject.AddHandler(FrameworkElement.RequestBringIntoViewEvent,
            new RequestBringIntoViewEventHandler(BringArchiveHeaderIntoView), handledEventsToo: true);
        AssociatedObject.AddHandler(TreeViewItem.SelectedEvent, new RoutedEventHandler(ArchiveFolderSelected), handledEventsToo: true);
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewMouseLeftButtonDown -=
            PreviewMouseLeftButtonDown;
        AssociatedObject.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent,
            new RequestBringIntoViewEventHandler(BringArchiveHeaderIntoView));
        AssociatedObject.RemoveHandler(TreeViewItem.SelectedEvent, new RoutedEventHandler(ArchiveFolderSelected));
        base.OnDetaching();
    }

    private void ArchiveFolderSelected(object sender, RoutedEventArgs args)
    {
        if(args.OriginalSource is not TreeViewItem item || item.DataContext is not ISystemItem data ||
           !ArchiveStorageProvider.IsArchive(data.Location)) return;
        item.Dispatcher.BeginInvoke(() =>
        {
            if(AssociatedObject is not null && item.IsLoaded && item.IsSelected) item.BringIntoView();
        }, DispatcherPriority.Loaded);
    }

    private void BringArchiveHeaderIntoView(object sender, RequestBringIntoViewEventArgs args)
    {
        if(adjustingArchiveVisibility || args.TargetObject is not TreeViewItem item ||
           item.DataContext is not ISystemItem data || !ArchiveStorageProvider.IsArchive(data.Location) ||
           item.Template?.FindName("Row", item) is not FrameworkElement row || row.ActualHeight <= 0)
            return;
        // Expanded items include their descendants and can be wider than the pane.
        // Scroll to the start of this row rather than the far edge of that whole subtree.
        args.Handled = true;
        adjustingArchiveVisibility = true;
        try
        {
            double width = Math.Max(1, AssociatedObject.ActualWidth - SystemParameters.VerticalScrollBarWidth);
            row.BringIntoView(new Rect(0, 0, Math.Min(row.ActualWidth, width), row.ActualHeight));
        }
        finally { adjustingArchiveVisibility = false; }
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

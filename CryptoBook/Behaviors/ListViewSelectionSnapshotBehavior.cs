using CryptoBook.Interfaces;
using CryptoBook.Services;

using Microsoft.Xaml.Behaviors;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CryptoBook.Behaviors
{
    public sealed class ListViewSelectionSnapshotBehavior:
        Behavior<System.Windows.Controls.ListView>
    {
        public static readonly DependencyProperty SelectionSnapshotProperty =
            DependencyProperty.Register(
                nameof(SelectionSnapshot),
                typeof(IReadOnlyList<ISystemItem>),
                typeof(ListViewSelectionSnapshotBehavior),
                new FrameworkPropertyMetadata(
                    Array.Empty<ISystemItem>(),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public IReadOnlyList<ISystemItem> SelectionSnapshot
        {
            get => (IReadOnlyList<ISystemItem>)GetValue(SelectionSnapshotProperty);
            set => SetValue(SelectionSnapshotProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectionChanged += OnSelectionChanged;
            AssociatedObject.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            CaptureSelection();
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
            AssociatedObject.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;
            base.OnDetaching();
        }

        private void OnSelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e) =>
            CaptureSelection();

        private void CaptureSelection()
        {
            SelectionSnapshot = FileExplorerSelectionPolicy.CreateSnapshot(
                AssociatedObject.SelectedItems);
        }

        private void OnPreviewMouseRightButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            System.Windows.Controls.ListViewItem? item =
                FindAncestor<System.Windows.Controls.ListViewItem>(
                e.OriginalSource as DependencyObject);
            if(item is null || item.IsSelected)
                return;

            AssociatedObject.SelectedItems.Clear();
            item.IsSelected = true;
            item.Focus();
        }

        private static T? FindAncestor<T>(DependencyObject? source)
            where T: DependencyObject
        {
            while(source is not null)
            {
                if(source is T result)
                    return result;
                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }
    }
}

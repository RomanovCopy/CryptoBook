using CryptoBook.DTO;
using CryptoBook.Interfaces;

using Microsoft.Xaml.Behaviors;

using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDataObject = System.Windows.DataObject;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfListView = System.Windows.Controls.ListView;
using WpfListViewItem = System.Windows.Controls.ListViewItem;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfTreeView = System.Windows.Controls.TreeView;
using WpfTreeViewItem = System.Windows.Controls.TreeViewItem;
using WpfVisual3D = System.Windows.Media.Media3D.Visual3D;

namespace CryptoBook.Behaviors
{
    public sealed class FileExplorerDragDropBehavior: Behavior<ItemsControl>
    {
        private const string InternalPathsFormat = "CryptoBook.FileExplorer.Paths";
        private static readonly TimeSpan AutoScrollInterval = TimeSpan.FromMilliseconds(180);
        private WpfPoint _dragStart;
        private DependencyObject? _dragSource;
        private DispatcherTimer? _autoScrollTimer;
        private ScrollViewer? _scrollViewer;
        private int _autoScrollDirection;

        public static readonly DependencyProperty DropCommandProperty =
            DependencyProperty.Register(
                nameof(DropCommand),
                typeof(ICommand),
                typeof(FileExplorerDragDropBehavior));

        public static readonly DependencyProperty DefaultDestinationPathProperty =
            DependencyProperty.Register(
                nameof(DefaultDestinationPath),
                typeof(string),
                typeof(FileExplorerDragDropBehavior),
                new PropertyMetadata(string.Empty));

        public ICommand? DropCommand
        {
            get => (ICommand?)GetValue(DropCommandProperty);
            set => SetValue(DropCommandProperty, value);
        }

        public string DefaultDestinationPath
        {
            get => (string)GetValue(DefaultDestinationPathProperty);
            set => SetValue(DefaultDestinationPathProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.AllowDrop = true;
            AssociatedObject.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += PreviewMouseMove;
            AssociatedObject.DragOver += DragOver;
            AssociatedObject.DragLeave += DragLeave;
            AssociatedObject.Drop += Drop;
            _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = AutoScrollInterval
            };
            _autoScrollTimer.Tick += AutoScrollTimerTick;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= PreviewMouseMove;
            AssociatedObject.DragOver -= DragOver;
            AssociatedObject.DragLeave -= DragLeave;
            AssociatedObject.Drop -= Drop;
            StopAutoScroll();
            if(_autoScrollTimer is not null)
                _autoScrollTimer.Tick -= AutoScrollTimerTick;
            _autoScrollTimer = null;
            _scrollViewer = null;
            base.OnDetaching();
        }

        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(AssociatedObject);
            _dragSource = FindItemContainer(e.OriginalSource as DependencyObject);
        }

        private void PreviewMouseMove(object sender, WpfMouseEventArgs e)
        {
            if(e.LeftButton != MouseButtonState.Pressed || _dragSource is null)
                return;

            WpfPoint current = e.GetPosition(AssociatedObject);
            if(Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
               Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            string[] paths = GetDraggedPaths(_dragSource);
            _dragSource = null;
            if(paths.Length == 0)
                return;

            var data = new WpfDataObject();
            data.SetData(InternalPathsFormat, paths);
            string[] nativePaths = paths.Select(ToNativePath).ToArray();
            if(nativePaths.All(Path.IsPathFullyQualified))
                data.SetData(WpfDataFormats.FileDrop, nativePaths);
            try
            {
                System.Windows.DragDrop.DoDragDrop(
                    AssociatedObject,
                    data,
                    WpfDragDropEffects.Copy | WpfDragDropEffects.Move);
            }
            finally
            {
                StopAutoScroll();
            }
        }

        private void DragOver(object sender, WpfDragEventArgs e)
        {
            if(e.Data.GetDataPresent(InternalPathsFormat) ||
               e.Data.GetDataPresent(WpfDataFormats.FileDrop))
            {
                UpdateAutoScroll(e.GetPosition(AssociatedObject));
            }
            else
            {
                StopAutoScroll();
            }

            if(!TryCreateRequest(e, out FileDropRequest request) ||
               DropCommand?.CanExecute(request) != true)
            {
                e.Effects = WpfDragDropEffects.None;
            }
            else
            {
                e.Effects = request.Operation == FileTransferKind.Copy
                    ? WpfDragDropEffects.Copy
                    : WpfDragDropEffects.Move;
            }

            e.Handled = true;
        }

        private void DragLeave(object sender, WpfDragEventArgs e)
        {
            WpfPoint pointer = e.GetPosition(AssociatedObject);
            if(pointer.X < 0 || pointer.Y < 0 ||
               pointer.X > AssociatedObject.ActualWidth ||
               pointer.Y > AssociatedObject.ActualHeight)
            {
                StopAutoScroll();
            }
        }

        private void Drop(object sender, WpfDragEventArgs e)
        {
            StopAutoScroll();
            if(TryCreateRequest(e, out FileDropRequest request) &&
               DropCommand?.CanExecute(request) == true)
            {
                DropCommand.Execute(request);
            }

            e.Handled = true;
        }

        private void UpdateAutoScroll(WpfPoint pointer)
        {
            _autoScrollDirection = GetAutoScrollDirection(
                pointer.Y,
                AssociatedObject.ActualHeight);
            if(_autoScrollDirection == 0)
            {
                StopAutoScroll();
                return;
            }

            _scrollViewer ??= FindVisualDescendant<ScrollViewer>(AssociatedObject);
            if(_scrollViewer is not null && _autoScrollTimer?.IsEnabled == false)
                _autoScrollTimer.Start();
        }

        private void AutoScrollTimerTick(object? sender, EventArgs e)
        {
            if(_scrollViewer is null || _autoScrollDirection == 0)
            {
                StopAutoScroll();
                return;
            }

            // Верхняя кромка двигает полотно вниз, нижняя — вверх.
            if(_autoScrollDirection < 0)
                _scrollViewer.LineUp();
            else
                _scrollViewer.LineDown();
        }

        private void StopAutoScroll()
        {
            _autoScrollDirection = 0;
            _autoScrollTimer?.Stop();
        }

        internal static int GetAutoScrollDirection(double pointerY, double viewportHeight)
        {
            if(viewportHeight <= 0 || double.IsNaN(pointerY))
                return 0;

            double edgeSize = Math.Min(48, viewportHeight / 3);
            if(pointerY <= edgeSize)
                return -1;
            if(pointerY >= viewportHeight - edgeSize)
                return 1;
            return 0;
        }

        private static T? FindVisualDescendant<T>(DependencyObject parent)
            where T: DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for(int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if(child is T match)
                    return match;

                T? descendant = FindVisualDescendant<T>(child);
                if(descendant is not null)
                    return descendant;
            }

            return null;
        }

        private bool TryCreateRequest(WpfDragEventArgs e, out FileDropRequest request)
        {
            request = null!;
            string[]? paths = e.Data.GetData(InternalPathsFormat) as string[];
            paths ??= e.Data.GetDataPresent(WpfDataFormats.FileDrop)
                ? e.Data.GetData(WpfDataFormats.FileDrop) as string[]
                : null;
            if(paths is null || paths.Length == 0)
                return false;

            string destination = ResolveDestination(e.OriginalSource as DependencyObject);
            if(string.IsNullOrWhiteSpace(destination))
                return false;

            FileTransferKind operation = (e.KeyStates & DragDropKeyStates.ControlKey) != 0
                ? FileTransferKind.Copy
                : FileTransferKind.Move;
            request = new FileDropRequest(paths, destination, operation);
            return true;
        }

        private string ResolveDestination(DependencyObject? source)
        {
            DependencyObject? container = FindItemContainer(source);
            return container is FrameworkElement { DataContext: IContainerSystemItem item }
                ? item.FullPath
                : DefaultDestinationPath;
        }

        private string[] GetDraggedPaths(DependencyObject source)
        {
            if(AssociatedObject is WpfListView listView)
            {
                if(source is FrameworkElement { DataContext: ISystemItem sourceItem } &&
                   !listView.SelectedItems.Contains(sourceItem))
                {
                    return [sourceItem.FullPath];
                }

                IEnumerable selectedItems = listView.SelectedItems;
                return selectedItems
                    .OfType<ISystemItem>()
                    .Select(item => item.FullPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return source is FrameworkElement { DataContext: ISystemItem item } &&
                   !string.IsNullOrWhiteSpace(item.FullPath)
                ? [item.FullPath]
                : [];
        }

        private DependencyObject? FindItemContainer(DependencyObject? source)
        {
            while(source is not null && !ReferenceEquals(source, AssociatedObject))
            {
                if(AssociatedObject is WpfListView && source is WpfListViewItem)
                    return source;
                if(AssociatedObject is WpfTreeView && source is WpfTreeViewItem)
                    return source;
                source = GetParent(source);
            }

            return null;
        }

        private static DependencyObject? GetParent(DependencyObject source) => source switch
        {
            Visual or WpfVisual3D => VisualTreeHelper.GetParent(source),
            FrameworkContentElement content => content.Parent,
            ContentElement content => ContentOperations.GetParent(content),
            _ => LogicalTreeHelper.GetParent(source)
        };

        private static string ToNativePath(string path) =>
            path.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                ? path[8..]
                : path;
    }
}

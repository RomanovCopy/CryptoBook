using CryptoBook.DTO;

using Microsoft.Xaml.Behaviors;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfControl = System.Windows.Controls.Control;
using WpfDataObject = System.Windows.DataObject;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfTreeView = System.Windows.Controls.TreeView;
using WpfVisual3D = System.Windows.Media.Media3D.Visual3D;

namespace CryptoBook.Behaviors
{
    /// <summary>
    /// Преобразует жесты TreeView в типизированные запросы перемещения. Правила
    /// структуры документа остаются в команде и доменном сервисе.
    /// </summary>
    public sealed class DocumentStructureDragDropBehavior: Behavior<WpfTreeView>
    {
        private const string NodeFormat = "CryptoBook.DocumentStructure.Node";
        private static readonly TimeSpan AutoScrollInterval =
            TimeSpan.FromMilliseconds(180);
        private static readonly TimeSpan AutoExpandDelay =
            TimeSpan.FromMilliseconds(650);

        private WpfPoint dragStart;
        private TreeViewItem? dragSource;
        private TreeViewItem? highlightedItem;
        private TreeViewItem? expandCandidate;
        private ScrollViewer? scrollViewer;
        private DispatcherTimer? autoScrollTimer;
        private DispatcherTimer? autoExpandTimer;
        private int autoScrollDirection;

        public static readonly DependencyProperty DropCommandProperty =
            DependencyProperty.Register(
                nameof(DropCommand),
                typeof(ICommand),
                typeof(DocumentStructureDragDropBehavior));

        public ICommand? DropCommand
        {
            get => (ICommand?)GetValue(DropCommandProperty);
            set => SetValue(DropCommandProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.AllowDrop = true;
            AssociatedObject.PreviewMouseLeftButtonDown +=
                PreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += PreviewMouseMove;
            AssociatedObject.DragOver += DragOver;
            AssociatedObject.DragLeave += DragLeave;
            AssociatedObject.Drop += Drop;

            autoScrollTimer = new DispatcherTimer(
                DispatcherPriority.Background,
                AssociatedObject.Dispatcher)
            {
                Interval = AutoScrollInterval
            };
            autoScrollTimer.Tick += AutoScrollTimerTick;

            autoExpandTimer = new DispatcherTimer(
                DispatcherPriority.Background,
                AssociatedObject.Dispatcher)
            {
                Interval = AutoExpandDelay
            };
            autoExpandTimer.Tick += AutoExpandTimerTick;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseLeftButtonDown -=
                PreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= PreviewMouseMove;
            AssociatedObject.DragOver -= DragOver;
            AssociatedObject.DragLeave -= DragLeave;
            AssociatedObject.Drop -= Drop;
            StopDragFeedback();

            if(autoScrollTimer is not null)
                autoScrollTimer.Tick -= AutoScrollTimerTick;
            if(autoExpandTimer is not null)
                autoExpandTimer.Tick -= AutoExpandTimerTick;
            autoScrollTimer = null;
            autoExpandTimer = null;
            scrollViewer = null;
            base.OnDetaching();
        }

        private void PreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs args)
        {
            dragStart = args.GetPosition(AssociatedObject);
            DependencyObject? original = args.OriginalSource as DependencyObject;
            dragSource = FindAncestor<WpfButtonBase>(original) is null
                ? FindTreeViewItem(original)
                : null;
        }

        private void PreviewMouseMove(object sender, WpfMouseEventArgs args)
        {
            if(args.LeftButton != MouseButtonState.Pressed ||
               dragSource?.DataContext is not DocumentStructureNode node)
            {
                return;
            }

            WpfPoint current = args.GetPosition(AssociatedObject);
            if(Math.Abs(current.X - dragStart.X) <
                   SystemParameters.MinimumHorizontalDragDistance &&
               Math.Abs(current.Y - dragStart.Y) <
                   SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            TreeViewItem sourceContainer = dragSource;
            dragSource = null;
            sourceContainer.IsSelected = true;
            var data = new WpfDataObject();
            data.SetData(NodeFormat, node);
            try
            {
                System.Windows.DragDrop.DoDragDrop(
                    sourceContainer,
                    data,
                    WpfDragDropEffects.Move);
            }
            finally
            {
                StopDragFeedback();
            }
        }

        private void DragOver(object sender, WpfDragEventArgs args)
        {
            UpdateAutoScroll(args.GetPosition(AssociatedObject));
            if(!TryCreateRequest(
                   args,
                   out DocumentStructureMoveRequest request,
                   out TreeViewItem targetItem) ||
               DropCommand?.CanExecute(request) != true)
            {
                args.Effects = WpfDragDropEffects.None;
                ClearHighlight();
                StopAutoExpand();
            }
            else
            {
                args.Effects = WpfDragDropEffects.Move;
                ShowHighlight(targetItem, request.Position);
                UpdateAutoExpand(targetItem, request.Position);
            }

            args.Handled = true;
        }

        private void DragLeave(object sender, WpfDragEventArgs args)
        {
            WpfPoint pointer = args.GetPosition(AssociatedObject);
            if(pointer.X < 0 ||
               pointer.Y < 0 ||
               pointer.X > AssociatedObject.ActualWidth ||
               pointer.Y > AssociatedObject.ActualHeight)
            {
                StopDragFeedback();
            }
        }

        private void Drop(object sender, WpfDragEventArgs args)
        {
            if(TryCreateRequest(
                   args,
                   out DocumentStructureMoveRequest request,
                   out _) &&
               DropCommand?.CanExecute(request) == true)
            {
                DropCommand.Execute(request);
            }

            StopDragFeedback();
            args.Handled = true;
        }

        private bool TryCreateRequest(
            WpfDragEventArgs args,
            out DocumentStructureMoveRequest request,
            out TreeViewItem targetItem)
        {
            request = null!;
            targetItem = null!;
            if(!args.Data.GetDataPresent(NodeFormat) ||
               args.Data.GetData(NodeFormat) is not DocumentStructureNode source)
            {
                return false;
            }

            targetItem = FindTreeViewItem(
                args.OriginalSource as DependencyObject)!;
            if(targetItem?.DataContext is not DocumentStructureNode target)
                return false;

            FrameworkElement header = targetItem.Template.FindName(
                "HeaderHost",
                targetItem) as FrameworkElement ?? targetItem;
            WpfPoint pointer = args.GetPosition(header);
            DocumentStructureDropPosition position = GetDropPosition(
                pointer.Y,
                header.ActualHeight);
            request = new DocumentStructureMoveRequest(
                source,
                target,
                position);
            return true;
        }

        private void ShowHighlight(
            TreeViewItem item,
            DocumentStructureDropPosition position)
        {
            if(!ReferenceEquals(highlightedItem, item))
                ClearHighlight();

            highlightedItem = item;
            item.BorderBrush = AssociatedObject.TryFindResource(
                "CurrentAccent") as WpfBrush ?? WpfBrushes.DodgerBlue;
            item.BorderThickness = position switch
            {
                DocumentStructureDropPosition.Before =>
                    new Thickness(0, 2, 0, 0),
                DocumentStructureDropPosition.After =>
                    new Thickness(0, 0, 0, 2),
                _ => new Thickness(2)
            };
        }

        private void ClearHighlight()
        {
            if(highlightedItem is null)
                return;
            highlightedItem.ClearValue(WpfControl.BorderBrushProperty);
            highlightedItem.ClearValue(WpfControl.BorderThicknessProperty);
            highlightedItem = null;
        }

        private void UpdateAutoScroll(WpfPoint pointer)
        {
            autoScrollDirection = GetAutoScrollDirection(
                pointer.Y,
                AssociatedObject.ActualHeight);
            if(autoScrollDirection == 0)
            {
                autoScrollTimer?.Stop();
                return;
            }

            scrollViewer ??= FindVisualDescendant<ScrollViewer>(
                AssociatedObject);
            if(scrollViewer is not null &&
               autoScrollTimer?.IsEnabled == false)
            {
                autoScrollTimer.Start();
            }
        }

        private void AutoScrollTimerTick(object? sender, EventArgs args)
        {
            if(scrollViewer is null || autoScrollDirection == 0)
            {
                autoScrollTimer?.Stop();
                return;
            }

            if(autoScrollDirection < 0)
                scrollViewer.LineUp();
            else
                scrollViewer.LineDown();
        }

        private void UpdateAutoExpand(
            TreeViewItem target,
            DocumentStructureDropPosition position)
        {
            if(position != DocumentStructureDropPosition.Inside ||
               target.IsExpanded ||
               !target.HasItems)
            {
                StopAutoExpand();
                return;
            }

            if(ReferenceEquals(expandCandidate, target))
                return;
            expandCandidate = target;
            autoExpandTimer?.Stop();
            autoExpandTimer?.Start();
        }

        private void AutoExpandTimerTick(object? sender, EventArgs args)
        {
            autoExpandTimer?.Stop();
            if(expandCandidate is not null)
                expandCandidate.IsExpanded = true;
            expandCandidate = null;
        }

        private void StopAutoExpand()
        {
            autoExpandTimer?.Stop();
            expandCandidate = null;
        }

        private void StopDragFeedback()
        {
            autoScrollDirection = 0;
            autoScrollTimer?.Stop();
            StopAutoExpand();
            ClearHighlight();
        }

        internal static DocumentStructureDropPosition GetDropPosition(
            double pointerY,
            double itemHeight)
        {
            if(itemHeight <= 0 || double.IsNaN(pointerY))
                return DocumentStructureDropPosition.Inside;
            if(pointerY < itemHeight / 3)
                return DocumentStructureDropPosition.Before;
            if(pointerY > itemHeight * 2 / 3)
                return DocumentStructureDropPosition.After;
            return DocumentStructureDropPosition.Inside;
        }

        internal static int GetAutoScrollDirection(
            double pointerY,
            double viewportHeight)
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

        private TreeViewItem? FindTreeViewItem(DependencyObject? source) =>
            FindAncestor<TreeViewItem>(source);

        private T? FindAncestor<T>(DependencyObject? source)
            where T: DependencyObject
        {
            while(source is not null &&
                  !ReferenceEquals(source, AssociatedObject))
            {
                if(source is T match)
                    return match;
                source = GetParent(source);
            }

            return null;
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

        private static DependencyObject? GetParent(DependencyObject source) =>
            source switch
            {
                Visual or WpfVisual3D => VisualTreeHelper.GetParent(source),
                FrameworkContentElement content => content.Parent,
                ContentElement content => ContentOperations.GetParent(content),
                _ => LogicalTreeHelper.GetParent(source)
            };
    }
}

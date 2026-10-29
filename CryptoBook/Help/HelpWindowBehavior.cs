using Microsoft.Xaml.Behaviors;
using CryptoBook.Infrastructure;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using TreeView = System.Windows.Controls.TreeView;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace CryptoBook.Help;

public static class HelpWindowPlacement
{
    public static Rect Clamp(HelpWindowState state, Rect workArea)
    {
        double width = double.IsFinite(state.Width) ? state.Width : 1120;
        double height = double.IsFinite(state.Height) ? state.Height : 780;
        width = Math.Clamp(width, Math.Min(720, workArea.Width), workArea.Width);
        height = Math.Clamp(height, Math.Min(480, workArea.Height), workArea.Height);
        double left = double.IsFinite(state.Left) ? state.Left : workArea.Left + (workArea.Width - width) / 2;
        double top = double.IsFinite(state.Top) ? state.Top : workArea.Top + (workArea.Height - height) / 2;
        return new(Math.Clamp(left, workArea.Left, workArea.Right - width),
            Math.Clamp(top, workArea.Top, workArea.Bottom - height), width, height);
    }
}

public sealed class HelpWindowBehavior : Behavior<Window>
{
    private HelpWindowViewModel? model;
    private ScrollViewer? scroll;
    private bool restoring;
    private IInputElement? imageFocus;
    protected override void OnAttached()
    {
        // Select the saved monitor before WPF creates the HWND and chooses its DPI.
        if (AssociatedObject.DataContext is HelpWindowViewModel viewModel)
        {
            var state = viewModel.SavedState;
            var desktop = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (double.IsFinite(state.Left) && double.IsFinite(state.Top) && desktop.Contains(new System.Windows.Point(state.Left, state.Top)))
            {
                AssociatedObject.WindowStartupLocation = WindowStartupLocation.Manual;
                AssociatedObject.Left = state.Left; AssociatedObject.Top = state.Top;
            }
        }
        AssociatedObject.Loaded += Loaded;
        AssociatedObject.Closed += Closed;
        AssociatedObject.PreviewKeyDown += KeyDown;
        AssociatedObject.SizeChanged += SizeChanged;
    }
    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= Loaded;
        AssociatedObject.Closed -= Closed;
        AssociatedObject.PreviewKeyDown -= KeyDown;
        AssociatedObject.SizeChanged -= SizeChanged;
        DetachModel();
    }
    private T Control<T>(string name) where T : class => (AssociatedObject.FindName(name) as T)!;
    private void Loaded(object sender, RoutedEventArgs e)
    {
        if (model is not null) return;
        model = AssociatedObject.DataContext as HelpWindowViewModel;
        if (model is null) return;
        Rect work = WindowManager.GetWorkArea(AssociatedObject);
        AssociatedObject.MinWidth = Math.Min(720, work.Width);
        AssociatedObject.MinHeight = Math.Min(480, work.Height);
        var saved = model.SavedState;
        var bounds = HelpWindowPlacement.Clamp(saved with
        {
            Left = double.IsFinite(saved.Left) ? saved.Left : AssociatedObject.Left,
            Top = double.IsFinite(saved.Top) ? saved.Top : AssociatedObject.Top
        }, work);
        AssociatedObject.Left = bounds.Left; AssociatedObject.Top = bounds.Top;
        AssociatedObject.Width = bounds.Width; AssociatedObject.Height = bounds.Height;
        Control<ColumnDefinition>("NavigationColumn").Width = new GridLength(double.IsFinite(model.SavedState.NavigationWidth)
            ? Math.Clamp(model.SavedState.NavigationWidth, 220, 420) : 280);
        model.PropertyChanged += ModelChanged;
        Control<TreeView>("ContentsTree").SelectedItemChanged += TopicSelected;
        Control<ListBox>("SearchResults").PreviewMouseLeftButtonUp += ResultClick;
        Control<FlowDocumentScrollViewer>("ArticleViewer").SizeChanged += SizeChanged;
        AssociatedObject.UpdateLayout();
        scroll = Descendant<ScrollViewer>(Control<FlowDocumentScrollViewer>("ArticleViewer"));
        if (scroll is not null) scroll.ScrollChanged += Scrolled;
        Resize(); RestoreScroll();
        Control<TreeView>("ContentsTree").Focus();
    }
    private void Closed(object? sender, EventArgs e)
    {
        if (model is not null)
        {
            Rect bounds = AssociatedObject.WindowState == WindowState.Normal
                ? new(AssociatedObject.Left, AssociatedObject.Top, AssociatedObject.ActualWidth, AssociatedObject.ActualHeight)
                : AssociatedObject.RestoreBounds;
            model.SaveWindowState(bounds, Control<ColumnDefinition>("NavigationColumn").ActualWidth);
        }
        DetachModel();
    }
    private void DetachModel()
    {
        if (model is null) return;
        model.PropertyChanged -= ModelChanged;
        Control<TreeView>("ContentsTree").SelectedItemChanged -= TopicSelected;
        Control<ListBox>("SearchResults").PreviewMouseLeftButtonUp -= ResultClick;
        Control<FlowDocumentScrollViewer>("ArticleViewer").SizeChanged -= SizeChanged;
        if (scroll is not null) scroll.ScrollChanged -= Scrolled;
        model.Dispose(); model = null;
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HelpWindowViewModel.Article)) RestoreScroll();
        if (e.PropertyName == nameof(HelpWindowViewModel.IsImageOpen))
        {
            if (model?.IsImageOpen == true)
            {
                imageFocus = Keyboard.FocusedElement;
                AssociatedObject.Dispatcher.BeginInvoke(() => Control<Button>("CloseImageButton").Focus(), DispatcherPriority.Loaded);
            }
            else { imageFocus?.Focus(); imageFocus = null; }
        }
    }
    private void RestoreScroll()
    {
        if (model is null) return;
        double offset = model.ScrollOffset;
        restoring = true;
        AssociatedObject.Dispatcher.BeginInvoke(() =>
        {
            if (model is null) return;
            scroll?.ScrollToVerticalOffset(offset);
            model.ScrollOffset = offset;
            restoring = false;
        }, DispatcherPriority.Background);
    }
    private void Scrolled(object sender, ScrollChangedEventArgs e)
    {
        if (!restoring && model is not null && scroll is not null) model.ScrollOffset = scroll.VerticalOffset;
    }
    private void TopicSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is HelpTreeNode { Children.Count: 0 } topic && model?.CurrentTopicId != topic.Id)
            model?.OpenTopic(topic.Id);
    }
    private void ResultClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(Control<ListBox>("SearchResults"), source) is ListBoxItem { DataContext: HelpSearchResult result })
            model?.OpenTopic(result.Id);
    }
    private void SizeChanged(object sender, SizeChangedEventArgs e) => Resize();
    private void Resize()
    {
        if (model is null) return;
        var column = Control<ColumnDefinition>("NavigationColumn");
        double max = Math.Max(220, Math.Min(420, AssociatedObject.ActualWidth - 390));
        column.MaxWidth = max;
        if (column.Width.Value > max) column.Width = new GridLength(max);
        model.ArticleWidth = Math.Max(160, Control<FlowDocumentScrollViewer>("ArticleViewer").ActualWidth - 64);
    }
    private void KeyDown(object sender, KeyEventArgs e)
    {
        if (model is null) return;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F1) { AssociatedObject.Activate(); e.Handled = true; }
        else if (key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        { Control<TextBox>("HelpSearch").Focus(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Left or Key.Right)
        {
            var command = key == Key.Left ? model.Back : model.Forward;
            if (command.CanExecute(null)) command.Execute(null);
            e.Handled = true;
        }
        else if (key == Key.Escape)
        {
            if (model.IsImageOpen) { model.CloseImage.Execute(null); e.Handled = true; }
            else if (Control<TextBox>("HelpSearch").IsKeyboardFocusWithin)
            {
                if (model.Query.Length > 0) model.Query = "";
                else Control<FlowDocumentScrollViewer>("ArticleViewer").Focus();
                e.Handled = true;
            }
        }
        else if (key == Key.Enter && (Control<TextBox>("HelpSearch").IsKeyboardFocusWithin || Control<ListBox>("SearchResults").IsKeyboardFocusWithin))
        {
            if (Control<TextBox>("HelpSearch").IsKeyboardFocusWithin) model.RunSearch();
            if (model.SelectedResult is { } result) model.OpenTopic(result.Id);
            e.Handled = true;
        }
    }
    private static T? Descendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (Descendant<T>(child) is { } nested) return nested;
        }
        return null;
    }
}

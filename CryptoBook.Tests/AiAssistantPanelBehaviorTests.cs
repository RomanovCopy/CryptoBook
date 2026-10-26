using CryptoBook.Behaviors;
using CryptoBook.Infrastructure;
using Microsoft.Xaml.Behaviors;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace CryptoBook.Tests;

public sealed class AiAssistantPanelBehaviorTests
{
    [WpfTheory]
    [InlineData(true, DragDropEffects.Copy, true)]
    [InlineData(false, DragDropEffects.Copy, false)]
    [InlineData(true, DragDropEffects.Move, false)]
    public void FileDrop_UsesCommandAvailabilityAndPassesPaths(bool canExecute, DragDropEffects allowed, bool accepted)
    {
        var target = new Border { AllowDrop = true };
        object? received = null;
        string[] paths = [@"C:\selected.txt"];
        var behavior = new FileDropCommandBehavior
        {
            Command = new RelayCommand(value => received = value, value => canExecute && value is string[] { Length: 1 })
        };
        Interaction.GetBehaviors(target).Add(behavior);
        var over = FileEvent(target, UIElement.PreviewDragOverEvent, paths, allowed);
        target.RaiseEvent(over);
        Assert.True(over.Handled);
        Assert.Equal(accepted ? DragDropEffects.Copy : DragDropEffects.None, over.Effects);
        Assert.Null(received);

        var drop = FileEvent(target, UIElement.PreviewDropEvent, paths, allowed);
        target.RaiseEvent(drop);
        Assert.True(drop.Handled);
        if(accepted) Assert.Equal(paths, Assert.IsType<string[]>(received));
        else Assert.Null(received);

        received = null;
        Interaction.GetBehaviors(target).Remove(behavior);
        var detached = FileEvent(target, UIElement.PreviewDropEvent, paths, allowed);
        target.RaiseEvent(detached);
        Assert.False(detached.Handled);
        Assert.Null(received);
    }

    [WpfFact]
    public async Task Resize_MarkedHandlePreservesPreferredWidthAcrossHostResizeAndReload()
    {
        const string xaml = """
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
                    xmlns:b="clr-namespace:CryptoBook.Behaviors;assembly=CryptoBook">
                <i:Interaction.Behaviors>
                    <b:PanelResizeBehavior/>
                </i:Interaction.Behaviors>
                <Grid>
                    <Thumb x:Name="Handle" Width="6" HorizontalAlignment="Left" b:PanelResizeBehavior.IsResizeHandle="True"/>
                    <Thumb x:Name="OtherHandle" Width="6" HorizontalAlignment="Right"/>
                </Grid>
            </Border>
            """;
        var panel = (Border)XamlReader.Parse(xaml);
        var handle = (Thumb)panel.FindName("Handle");
        var behavior = Assert.Single(Interaction.GetBehaviors(panel).OfType<PanelResizeBehavior>());
        var firstGrid = HostGrid(panel);
        var window = HostWindow(firstGrid);
        try
        {
            window.Show();
            await Layout(window);
            Assert.Equal(390, panel.ActualWidth, 1);
            var unrelated = (Thumb)panel.FindName("OtherHandle");
            unrelated.RaiseEvent(new DragDeltaEventArgs(-100, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            await Layout(window);
            Assert.Equal(390, panel.ActualWidth, 1);
            handle.RaiseEvent(new DragDeltaEventArgs(-100, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            await Layout(window);
            Assert.Equal(490, panel.ActualWidth, 1);

            window.Width = 600;
            await Layout(window);
            Assert.Equal(280, panel.ActualWidth, 1);
            window.Width = 1000;
            await Layout(window);
            Assert.Equal(490, panel.ActualWidth, 1);

            window.Content = null;
            await Layout(window);
            firstGrid.Children.Remove(panel);
            var secondGrid = HostGrid(panel);
            window.Content = secondGrid;
            window.Width = 800;
            await Layout(window);
            Assert.Equal(secondGrid.ActualWidth - secondGrid.ColumnDefinitions[0].ActualWidth - behavior.MinimumRemainingWidth,
                panel.ActualWidth, 1);
            window.Width = 1000;
            await Layout(window);
            Assert.Equal(490, panel.ActualWidth, 1);

            Interaction.GetBehaviors(panel).Remove(behavior);
            handle.RaiseEvent(new DragDeltaEventArgs(-100, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            window.Width = 600;
            await Layout(window);
            Assert.Equal(490, panel.Width, 1);
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public async Task SlideIn_AnimatesAfterLayoutAndStopsWhenHiddenOrDetached()
    {
        var sourceTransform = new TranslateTransform();
        sourceTransform.Freeze();
        var panel = new Border { Width = 200, RenderTransform = sourceTransform };
        var behavior = new SlideInBehavior { Duration = TimeSpan.FromSeconds(10) };
        Interaction.GetBehaviors(panel).Add(behavior);
        var window = HostWindow(panel);
        try
        {
            window.Show();
            await Layout(window);
            var transform = Assert.IsType<TranslateTransform>(panel.RenderTransform);
            Assert.NotSame(sourceTransform, transform);
            Assert.True(transform.HasAnimatedProperties);
            Assert.Equal(200, panel.ActualWidth, 1);
            panel.Visibility = Visibility.Collapsed;
            Assert.False(transform.HasAnimatedProperties);
            Assert.Equal(0, transform.X);
            panel.Visibility = Visibility.Visible;
            await Layout(window);
            Assert.True(transform.HasAnimatedProperties);
            Interaction.GetBehaviors(panel).Remove(behavior);
            Assert.False(transform.HasAnimatedProperties);
            panel.Visibility = Visibility.Collapsed;
            panel.Visibility = Visibility.Visible;
            await Layout(window);
            Assert.False(transform.HasAnimatedProperties);
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public async Task ScrollToEnd_FollowsStreamingContentButPreservesManualScrollPosition()
    {
        var content = new Border { Height = 600 };
        var viewer = new ScrollViewer { Content = content, Height = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var behavior = new ScrollToEndBehavior();
        Interaction.GetBehaviors(viewer).Add(behavior);
        var window = HostWindow(viewer);
        try
        {
            window.Show();
            await Layout(window);
            Assert.Equal(viewer.ScrollableHeight, viewer.VerticalOffset, 1);
            content.Height = 800;
            await Layout(window);
            Assert.Equal(viewer.ScrollableHeight, viewer.VerticalOffset, 1);
            viewer.ScrollToVerticalOffset(200);
            await Layout(window);
            content.Height = 1000;
            await Layout(window);
            Assert.Equal(200, viewer.VerticalOffset, 1);
            viewer.ScrollToEnd();
            await Layout(window);
            content.Height = 1200;
            await Layout(window);
            Assert.Equal(viewer.ScrollableHeight, viewer.VerticalOffset, 1);
            double offset = viewer.VerticalOffset;
            Interaction.GetBehaviors(viewer).Remove(behavior);
            // ScrollToEnd stores an infinite desired offset in WPF; replace it with a finite offset.
            viewer.ScrollToVerticalOffset(offset);
            await Layout(window);
            content.Height = 1400;
            await Layout(window);
            Assert.Equal(offset, viewer.VerticalOffset, 1);
        }
        finally { window.Close(); }
    }

    private static DragEventArgs FileEvent(DependencyObject target, RoutedEvent routedEvent, string[] paths, DragDropEffects allowed)
    {
        // WPF creates DragEventArgs internally; raising the real routed event also checks event subscriptions.
        var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [new DataObject(DataFormats.FileDrop, paths), DragDropKeyStates.None, allowed, target, new Point()], null)!;
        args.RoutedEvent = routedEvent;
        return args;
    }
    private static Grid HostGrid(FrameworkElement panel)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Grid.SetColumn(panel, 2);
        grid.Children.Add(panel);
        return grid;
    }
    private static Window HostWindow(object content) => new()
    {
        Content = content, Width = 1000, Height = 240, WindowStyle = WindowStyle.None,
        ShowInTaskbar = false, ShowActivated = false, Left = -10_000, Top = -10_000
    };
    private static async Task Layout(Window window)
    {
        window.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        window.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
    }
}

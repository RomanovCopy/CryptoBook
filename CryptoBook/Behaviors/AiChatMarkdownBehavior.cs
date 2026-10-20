using CryptoBook.AI;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace CryptoBook.Behaviors;

/// <summary>Renders message text on the UI thread, coalescing streamed fragments.</summary>
public static class AiChatMarkdownBehavior
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(AiChatMarkdownBehavior), new PropertyMetadata(null, OnChanged));
    public static readonly DependencyProperty RenderMarkdownProperty = DependencyProperty.RegisterAttached(
        "RenderMarkdown", typeof(bool), typeof(AiChatMarkdownBehavior), new PropertyMetadata(true, OnChanged));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(RenderState), typeof(AiChatMarkdownBehavior));

    public static string GetText(DependencyObject target) => (string?)target.GetValue(TextProperty) ?? "";
    public static void SetText(DependencyObject target, string value) => target.SetValue(TextProperty, value);
    public static bool GetRenderMarkdown(DependencyObject target) => (bool)target.GetValue(RenderMarkdownProperty);
    public static void SetRenderMarkdown(DependencyObject target, bool value) => target.SetValue(RenderMarkdownProperty, value);

    private static void OnChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if(target is not RichTextBox viewer) return;
        var state = (RenderState?)viewer.GetValue(StateProperty);
        if(state is null)
        {
            state = new(viewer);
            viewer.SetValue(StateProperty, state);
        }
        if(e.Property == RenderMarkdownProperty) state.Refresh(); else state.Schedule();
    }

    private sealed class RenderState
    {
        private static readonly DependencyProperty[] Formatting =
            [RichTextBox.FontFamilyProperty, RichTextBox.FontSizeProperty, RichTextBox.ForegroundProperty];
        private readonly RichTextBox viewer;
        private readonly DispatcherTimer timer;
        private bool listening;

        internal RenderState(RichTextBox viewer)
        {
            this.viewer = viewer;
            timer = new DispatcherTimer(DispatcherPriority.Background, viewer.Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += OnTick;
            viewer.Loaded += OnLoaded;
            viewer.Unloaded += OnUnloaded;
            viewer.IsVisibleChanged += OnVisibilityChanged;
            if(viewer.IsLoaded) Start();
        }

        internal void Schedule()
        {
            if(viewer.IsLoaded && viewer.IsVisible && !timer.IsEnabled) timer.Start();
        }
        internal void Refresh() { timer.Stop(); Render(); }

        private void Start()
        {
            if(!listening)
            {
                foreach(var property in Formatting)
                    DependencyPropertyDescriptor.FromProperty(property, typeof(RichTextBox)).AddValueChanged(viewer, OnFormattingChanged);
                listening = true;
            }
            Render();
        }
        private void OnLoaded(object sender, RoutedEventArgs e) => Start();
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            if(!listening) return;
            foreach(var property in Formatting)
                DependencyPropertyDescriptor.FromProperty(property, typeof(RichTextBox)).RemoveValueChanged(viewer, OnFormattingChanged);
            listening = false;
        }
        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(viewer.IsVisible) Schedule(); else timer.Stop();
        }
        private void OnFormattingChanged(object? sender, EventArgs e) => Schedule();
        private void OnTick(object? sender, EventArgs e) { timer.Stop(); Render(); }
        private void Render()
        {
            if(!viewer.IsLoaded || !viewer.IsVisible) return;
            viewer.Document = AiChatDocumentRenderer.Render(GetText(viewer), GetRenderMarkdown(viewer),
                viewer.FontFamily, viewer.FontSize, viewer.Foreground);
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CryptoBook.MyControls;

public partial class AiAssistantPanel : System.Windows.Controls.UserControl
{
    private bool followAnswer = true;
    private const double MinimumPanelWidth = 280, MinimumEditorWidth = 220;
    private double preferredWidth = 390;
    private Grid? hostGrid;

    public AiAssistantPanel()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) =>
        {
            if(!IsVisible) return;
            FitToHost();
            ((TranslateTransform)RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(Width, 0, TimeSpan.FromMilliseconds(180)));
        };
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        if(hostGrid is not null) hostGrid.SizeChanged -= HostSizeChanged;
        base.OnVisualParentChanged(oldParent);
        hostGrid = VisualTreeHelper.GetParent(this) as Grid;
        if(hostGrid is not null) hostGrid.SizeChanged += HostSizeChanged;
        FitToHost();
    }

    private void HostSizeChanged(object sender, SizeChangedEventArgs e) => FitToHost();

    private (double Min, double Max) WidthLimits()
    {
        if(hostGrid is null || hostGrid.ActualWidth <= 0) return (MinimumPanelWidth, double.MaxValue);
        double available = hostGrid.ActualWidth;
        int panelColumn = Grid.GetColumn(this);
        for(int i = 0; i < hostGrid.ColumnDefinitions.Count; i++)
            if(i != panelColumn && !hostGrid.ColumnDefinitions[i].Width.IsStar)
                available -= hostGrid.ColumnDefinitions[i].ActualWidth;
        available = Math.Max(0, available);
        double minimum = Math.Min(MinimumPanelWidth, available);
        return (minimum, Math.Max(minimum, available - MinimumEditorWidth));
    }

    private void FitToHost()
    {
        var (minimum, maximum) = WidthLimits();
        SetCurrentValue(WidthProperty, Math.Clamp(preferredWidth, minimum, maximum));
    }

    private void ResizePanel(object sender, DragDeltaEventArgs e)
    {
        var (minimum, maximum) = WidthLimits();
        preferredWidth = Math.Clamp(ActualWidth - e.HorizontalChange, minimum, maximum);
        FitToHost();
        e.Handled = true;
    }

    private void FilesDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if(!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;
        e.Effects = DataContext is CryptoBook.AI.AiAssistantViewModel { CanAttach: true } ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void FilesDropped(object sender, System.Windows.DragEventArgs e)
    {
        if(!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) || DataContext is not CryptoBook.AI.AiAssistantViewModel assistant) return;
        e.Handled = true;
        if(e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths) await assistant.AttachPathsAsync(paths);
    }

    private void ConversationScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if(e.ExtentHeightChange == 0)
            followAnswer = ConversationScroll.VerticalOffset >= ConversationScroll.ScrollableHeight - 2;
        if(followAnswer && e.ExtentHeightChange != 0) ConversationScroll.ScrollToEnd();
    }
}

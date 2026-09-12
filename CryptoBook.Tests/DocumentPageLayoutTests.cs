using CryptoBook.Behaviors;
using CryptoBook.Services;
using CryptoBook.DTO;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentPageLayoutTests
{
    [WpfFact]
    public void Apply_DefaultsToA4WithUnlimitedHeight()
    {
        var document = new FlowDocument();

        DocumentPageLayout.Apply(document);

        Assert.Equal(210 * 96 / 25.4, document.PageWidth);
        Assert.True(double.IsNaN(document.PageHeight));
        Assert.Equal(document.PageWidth, document.MinPageWidth);
        Assert.Equal(document.PageWidth, document.MaxPageWidth);
        Assert.Equal(DocumentPageLayout.PagePadding, document.PagePadding);
        Assert.True(double.IsPositiveInfinity(document.ColumnWidth));
    }

    [WpfTheory]
    [InlineData(DocumentPaperSize.A2, false, 420)]
    [InlineData(DocumentPaperSize.A2, true, 594)]
    [InlineData(DocumentPaperSize.A3, false, 297)]
    [InlineData(DocumentPaperSize.A3, true, 420)]
    [InlineData(DocumentPaperSize.A4, false, 210)]
    [InlineData(DocumentPaperSize.A4, true, 297)]
    public async Task PaperWidth_SurvivesSerializationAndPreview(
        DocumentPaperSize size, bool landscape, double millimeters)
    {
        var document = new FlowDocument(new Paragraph(new Run("page content")));
        DocumentPageLayout.Apply(document);
        DocumentPageLayout.Apply(document, size, landscape);
        var handler = new XamlPackageDocumentFormatHandler(
            new WpfDispatcherService(System.Windows.Threading.Dispatcher.CurrentDispatcher));
        byte[] content = await handler.SerializeAsync(document);
        var restored = new FlowDocument();
        await handler.LoadAsync(restored, content);
        DocumentPageLayout.Apply(restored);
        DocumentPageLayout.Apply(restored);

        Assert.Equal(millimeters * 96 / 25.4, restored.PageWidth, 6);
        Assert.Equal(restored.PageWidth, restored.MinPageWidth);
        Assert.Equal(restored.PageWidth, restored.MaxPageWidth);
        Assert.True(double.IsNaN(restored.PageHeight));
        var preview = new DocumentPreviewService().CreatePreview(restored);
        Assert.Equal(restored.PageWidth, preview.PageWidth);
        Assert.True(double.IsNaN(preview.PageHeight));
    }

    [WpfFact]
    public void Editor_KeepsPageWidthWhenViewportNarrowsAndContentGrows()
    {
        var document = new FlowDocument();
        DocumentPageLayout.Apply(document, DocumentPaperSize.A3, true);
        for(int i = 0; i < 100; i++)
            document.Blocks.Add(new Paragraph(new Run(new string('x', 120))));
        var editor = new RichTextBox(document)
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0), BorderThickness = new Thickness(0)
        };
        editor.Measure(new Size(600, 400));
        editor.Arrange(new Rect(0, 0, 600, 400));
        editor.UpdateLayout();
        Assert.InRange(editor.ExtentWidth, document.PageWidth - 1, document.PageWidth + 1);
        Assert.True(editor.ExtentHeight > 400);
        Assert.True(editor.ExtentWidth > editor.ViewportWidth);
        Assert.True(double.IsNaN(document.PageHeight));
    }

    [WpfTheory]
    [InlineData(600)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void Editor_DrawsThreePageEdgesWithoutBottomEdge(int width)
    {
        var document = new FlowDocument(new Paragraph(new Run("A4 — страница с неограниченной высотой")));
        DocumentPageLayout.Apply(document);
        var editor = new RichTextBox(document)
        {
            Padding = new Thickness(0), BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Black, Background = Brushes.White,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var host = new AdornerDecorator { Child = editor };
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        host.Measure(new Size(width, 400));
        host.Arrange(new Rect(0, 0, width, 400));
        host.UpdateLayout();
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        host.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, 400, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var pixels = new byte[width * 400 * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        byte Red(int x, int y) => pixels[(y * width + x) * 4 + 2];
        const int gutter = 24;
        Assert.True(Red(gutter, 200) < 128, "Left page edge is visible.");
        Assert.True(Red(400, gutter) < 128, "Top page edge is visible.");
        Assert.True(Red(width - gutter - 1, 200) < 128, "Right page edge is visible.");
        Assert.Equal(255, pixels[(200 * width + gutter) * 4 + 3]);
        Assert.Equal(255, pixels[(200 * width + width - gutter - 1) * 4 + 3]);
        Assert.Equal(255, Red(400, 399));
        if(Environment.GetEnvironmentVariable("CRYPTOBOOK_UI_QA_DIR") is { Length: > 0 } outputDirectory)
        {
            System.IO.Directory.CreateDirectory(outputDirectory);
            using var stream = System.IO.File.Create(System.IO.Path.Combine(outputDirectory, $"page-editor-{width}.png"));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    [WpfFact]
    public void FitToWindow_UsesSmallerViewportDimension()
    {
        double zoom = DocumentPageFitBehavior.CalculateZoom(
            new Size(1200, 800),
            new Size(800, 1100),
            minimumZoom: 20,
            maximumZoom: 400);

        Assert.Equal(64, zoom);
    }

    [WpfFact]
    public async Task ZoomIndicator_TracksEditorResizeAndPreviewZoom()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "CryptoBook", "MyControls", "Richtextbox.xaml")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var source = XDocument.Load(Path.Combine(directory.FullName, "CryptoBook", "MyControls", "Richtextbox.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement indicatorXaml = source.Descendants().Single(element => (string?)element.Attribute(x + "Name") == "ZoomIndicator");
        var resetButtonXaml = new XElement(source.Descendants().Single(element => (string?)element.Attribute(x + "Name") == "ResetZoomButton"));
        resetButtonXaml.Attribute("ToolTip")!.Remove();
        var grid = (Grid)XamlReader.Parse($$"""
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:behaviors="clr-namespace:CryptoBook.Behaviors;assembly=CryptoBook">
                <Grid.Resources><Style TargetType="Button"/></Grid.Resources>
                <Grid.RowDefinitions><RowDefinition/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                <AdornerDecorator><ContentControl x:Name="EditorHost"/></AdornerDecorator>
                <FlowDocumentPageViewer x:Name="PageViewer" Visibility="Collapsed"/>
                <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="24,4">{{indicatorXaml}}{{resetButtonXaml}}</StackPanel>
            </Grid>
            """);
        var editorHost = (ContentControl)grid.FindName("EditorHost");
        var indicator = (TextBlock)grid.FindName("ZoomIndicator");
        var preview = (FlowDocumentPageViewer)grid.FindName("PageViewer");
        var resetButton = (Button)grid.FindName("ResetZoomButton");
        var document = new FlowDocument(new Paragraph(new Run("Scaled page")));
        DocumentPageLayout.Apply(document);
        var editor = new RichTextBox(document)
        {
            Padding = new Thickness(0), BorderThickness = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        editorHost.Content = editor;
        grid.DataContext = new { IsPreviewMode = false };
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        await Resize(1000);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        await Resize(1000);
        Assert.Equal("120%", indicator.Text.Replace(" ", ""));
        await Resize(600);
        Assert.Equal("70%", indicator.Text.Replace(" ", ""));

        Assert.True(DocumentPageBorderBehavior.HandleMouseWheel(editor, 120, ModifierKeys.Control));
        await Resize(600);
        Assert.Equal("77%", indicator.Text.Replace(" ", ""));
        Assert.True(DocumentPageBorderBehavior.HandleMouseWheel(editor, -120, ModifierKeys.Control));
        await Resize(600);
        Assert.Equal("70%", indicator.Text.Replace(" ", ""));

        await ResetZoom();
        Assert.Equal("100%", indicator.Text.Replace(" ", ""));
        Assert.Equal(1, editor.LayoutTransform.Value.M11, 6);
        await Resize(1000);
        Assert.NotEqual("100%", indicator.Text.Replace(" ", ""));
        await ResetZoom();
        Assert.Equal("100%", indicator.Text.Replace(" ", ""));
        Assert.Equal(1, editor.LayoutTransform.Value.M11, 6);

        grid.DataContext = new { IsPreviewMode = true };
        preview.Zoom = 85;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Assert.Equal("85%", indicator.Text);
        Assert.Equal(Visibility.Collapsed, resetButton.Visibility);
        preview.Zoom = 100;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Assert.Equal("100%", indicator.Text);
        grid.DataContext = new { IsPreviewMode = false };
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Assert.Equal("100%", indicator.Text.Replace(" ", ""));
        Assert.Equal(Visibility.Visible, resetButton.Visibility);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        async Task ResetZoom()
        {
            Assert.Same(editor, resetButton.CommandTarget);
            Assert.True(DocumentPageBorderBehavior.ResetZoomCommand.CanExecute(null, editor));
            var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(resetButton);
            var invoke = (System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(
                System.Windows.Automation.Peers.PatternInterface.Invoke);
            invoke.Invoke();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            grid.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }

        async Task Resize(double width)
        {
            grid.Measure(new Size(width, 400));
            grid.Arrange(new Rect(0, 0, width, 400));
            grid.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }
    }

    [WpfFact]
    public void Editor_ControlWheelZoomPreservesDocumentAndSurvivesResizeAndReload()
    {
        var document = new FlowDocument(new Paragraph(new Run("Page zoom")));
        DocumentPageLayout.Apply(document);
        var editor = new RichTextBox(document)
        {
            Padding = new Thickness(0), BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Black, Background = Brushes.White,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var host = new AdornerDecorator { Child = new ContentControl { Content = editor } };
        double pageWidth = document.PageWidth;
        double fontSize = document.FontSize;
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        Layout(1000);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        host.UpdateLayout();
        double initialScale = editor.LayoutTransform.Value.M11;
        string originalContent = XamlWriter.Save(document);

        Assert.False(DocumentPageBorderBehavior.HandleMouseWheel(editor, 120, ModifierKeys.None));
        Assert.False(DocumentPageBorderBehavior.HandleMouseWheel(editor, 120, ModifierKeys.Shift));
        Assert.False(DocumentPageBorderBehavior.HandleMouseWheel(editor, 0, ModifierKeys.Control));
        Assert.Equal(initialScale, editor.LayoutTransform.Value.M11);
        Assert.True(DocumentPageBorderBehavior.HandleMouseWheel(editor, 240, ModifierKeys.Control));
        host.UpdateLayout();
        Assert.Equal(initialScale * 1.21, editor.LayoutTransform.Value.M11, 6);
        Assert.True(editor.ExtentWidth > editor.ViewportWidth);
        editor.ScrollToHorizontalOffset(50);
        host.UpdateLayout();
        Assert.True(editor.HorizontalOffset > 0);
        Assert.Equal(pageWidth, document.PageWidth);
        Assert.Equal(fontSize, document.FontSize);
        Assert.Equal(originalContent, XamlWriter.Save(document));

        Layout(600);
        Assert.Equal((600 - 48) / pageWidth * 1.21, editor.LayoutTransform.Value.M11, 6);
        Capture("page-zoom-in");
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        host.UpdateLayout();
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        host.UpdateLayout();
        Assert.Equal((600 - 48) / pageWidth * 1.21, editor.LayoutTransform.Value.M11, 6);
        Assert.True(DocumentPageBorderBehavior.HandleMouseWheel(editor, -240, ModifierKeys.Control));
        host.UpdateLayout();
        Assert.Equal((600 - 48) / pageWidth, editor.LayoutTransform.Value.M11, 6);

        Assert.True(DocumentPageBorderBehavior.HandleMouseWheel(editor, -120, ModifierKeys.Control));
        host.UpdateLayout();
        var bitmap = Capture("page-zoom-out");
        var pixels = new byte[600 * 400 * 4];
        bitmap.CopyPixels(pixels, 600 * 4, 0);
        int rightEdge = (int)(24 + (600 - 48) / 1.1);
        Assert.True(Enumerable.Range(rightEdge - 2, 4).Any(x => pixels[(200 * 600 + x) * 4 + 2] < 128),
            "The rendered right page edge follows the wheel zoom.");

        Assert.True(DocumentPageBorderBehavior.HandleMouseWheel(editor, int.MinValue, ModifierKeys.Control));
        host.UpdateLayout();
        Assert.Equal((600 - 48) / pageWidth * 0.1, editor.LayoutTransform.Value.M11, 6);
        Assert.True(DocumentPageBorderBehavior.HandleMouseWheel(editor, int.MaxValue, ModifierKeys.Control));
        host.UpdateLayout();
        Assert.Equal((600 - 48) / pageWidth * 5, editor.LayoutTransform.Value.M11, 6);
        DocumentPageBorderBehavior.SetIsEnabled(editor, false);
        Assert.True(editor.LayoutTransform.Value.IsIdentity);
        Assert.False(DocumentPageBorderBehavior.HandleMouseWheel(editor, 120, ModifierKeys.Control));

        RenderTargetBitmap Capture(string name)
        {
            var bitmap = new RenderTargetBitmap(600, 400, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            if(Environment.GetEnvironmentVariable("CRYPTOBOOK_UI_QA_DIR") is { Length: > 0 } outputDirectory)
            {
                Directory.CreateDirectory(outputDirectory);
                using var stream = File.Create(Path.Combine(outputDirectory, name + ".png"));
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
            return bitmap;
        }

        void Layout(double width)
        {
            host.Measure(new Size(width, 400));
            host.Arrange(new Rect(0, 0, width, 400));
            host.UpdateLayout();
        }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Editor_ScalesPaperWithFixedGuttersAfterResizeAndDocumentChange(bool longDocument)
    {
        var document = new FlowDocument();
        DocumentPageLayout.Apply(document);
        for(int i = 0; i < (longDocument ? 100 : 1); i++)
            document.Blocks.Add(new Paragraph(new Run("Page content")));
        var editor = new RichTextBox(document)
        {
            Padding = new Thickness(0), BorderThickness = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var content = new ContentControl { Content = editor };
        var host = new AdornerDecorator { Child = content };
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        Layout(1200);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        host.UpdateLayout();
        AssertFitted(1200);

        Layout(1050);
        AssertFitted(1050);
        Layout(600);
        AssertFitted(600);
        Layout(1500);
        AssertFitted(1500);
        Layout(2000);
        AssertFitted(2000);
        Layout(1050);
        if(longDocument)
        {
            editor.ScrollToVerticalOffset(100);
            host.UpdateLayout();
            Assert.True(editor.VerticalOffset > 0);
            AssertFitted(1050);
        }

        var wideDocument = new FlowDocument(new Paragraph(new Run("Wide page")));
        DocumentPageLayout.Apply(wideDocument, DocumentPaperSize.A2, true);
        editor.Document = wideDocument;
        host.UpdateLayout();
        AssertFitted(1050);
        editor.ScrollToHorizontalOffset(200);
        host.UpdateLayout();
        Assert.Equal(0, editor.HorizontalOffset, 4);
        Assert.Equal(DocumentPageLayout.GetWidth(DocumentPaperSize.A2, true), wideDocument.PageWidth);

        editor.Document = document;
        host.UpdateLayout();
        AssertFitted(1050);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(new Thickness(0), editor.Margin);
        Assert.True(editor.LayoutTransform.Value.IsIdentity);

        void Layout(double width)
        {
            host.Measure(new Size(width, 800));
            host.Arrange(new Rect(0, 0, width, 800));
            host.UpdateLayout();
        }

        void AssertFitted(double width)
        {
            Point origin = editor.TranslatePoint(new Point(), host);
            Point rightEdge = editor.TranslatePoint(new Point(editor.Document.PageWidth, 0), host);
            Assert.Equal(24, origin.X, 4);
            Assert.Equal(24, origin.Y, 4);
            Assert.True(Math.Abs(24 - (width - rightEdge.X)) < 0.0001,
                $"Width={width}, host={content.ActualWidth}, editor={editor.ActualWidth}, viewport={editor.ViewportWidth}, scale={editor.LayoutTransform.Value.M11}, right={rightEdge.X}, margin={editor.Margin}");
            Assert.InRange(editor.ViewportWidth, editor.Document.PageWidth - 0.01, editor.Document.PageWidth + 0.01);
            Assert.Equal(DocumentPageLayout.GetWidth(DocumentPaperSize.A4), document.PageWidth);
            Assert.True(double.IsNaN(editor.Document.PageHeight));
            Point controlRight = editor.TranslatePoint(new Point(editor.ActualWidth, 0), host);
            Assert.True(controlRight.X <= width, "The scrollbar stays inside the viewport at large zoom.");
        }
    }

    [WpfFact]
    public void Preview_DoesNotResizeImagesToFixedPage()
    {
        var source = new FlowDocument();
        DocumentPageLayout.Apply(source);

        for(int index = 0; index < 3; index++)
        {
            var image = new Image
            {
                Source = CreateTallBitmap(),
                Stretch = Stretch.Uniform
            };

            source.Blocks.Add(
                new Paragraph(new InlineUIContainer(image))
                {
                    Margin = new Thickness(0)
                });
        }

        FlowDocument preview =
            new DocumentPreviewService().CreatePreview(source);
        Image[] previewImages = preview.Blocks
            .OfType<Paragraph>()
            .SelectMany(paragraph => paragraph.Inlines)
            .OfType<InlineUIContainer>()
            .Select(container => container.Child)
            .OfType<Image>()
            .ToArray();
        Assert.Equal(3, previewImages.Length);
        foreach(Image image in previewImages)
        {
            Assert.InRange(image.Source.Width, 999, 1001);
            Assert.InRange(image.Source.Height, 1999, 2001);
            Assert.True(double.IsNaN(image.Width));
            Assert.True(double.IsNaN(image.Height));
        }
    }

    [WpfFact]
    public void Preview_PreservesPaperBackground()
    {
        var source = new FlowDocument(new Paragraph(new Run("text")))
        {
            Background = Brushes.Bisque
        };

        FlowDocument preview =
            new DocumentPreviewService().CreatePreview(source);

        Assert.Equal(
            Colors.Bisque,
            Assert.IsType<SolidColorBrush>(preview.Background).Color);
    }

    private static BitmapSource CreateTallBitmap()
    {
        const int width = 1000;
        const int height = 2000;
        int stride = (width + 7) / 8;
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.BlackWhite,
            null,
            new byte[stride * height],
            stride);
        bitmap.Freeze();
        return bitmap;
    }
}

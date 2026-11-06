using CryptoBook.Adorners;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Xunit;

namespace CryptoBook.Tests;

public sealed class BookmarkNavigationTests
{
    [WpfTheory]
    [InlineData(0.75)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public async Task NavigateTo_CentersTargetFromAboveBelowAndAlreadyVisible(double scale)
    {
        using var host = new EditorHost(scale);
        await Settle();
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        await Settle();
        AssertCentered(host);

        host.Editor.Service.ScrollToEnd();
        await Settle();
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        await Settle();
        AssertCentered(host);

        // BringIntoView alone does nothing when the target is already visible.
        host.Editor.Service.ScrollToVerticalOffset(host.Editor.Service.VerticalOffset + 60);
        await Settle();
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        await Settle();
        AssertCentered(host);
    }

    [WpfFact]
    public async Task NextPreviousAndWrap_CenterOrClampAtDocumentEdges()
    {
        using var host = new EditorHost();
        await Settle();
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "First"));
        await Settle();
        Assert.InRange(host.Editor.Service.VerticalOffset, 0, 1);
        Assert.True(host.Bookmarks.NavigateNext(host.Editor));
        await Settle();
        AssertCentered(host);
        Assert.True(host.Bookmarks.NavigateNext(host.Editor));
        await Settle();
        double maximum = host.Editor.Service.ExtentHeight - host.Editor.Service.ViewportHeight;
        Assert.InRange(host.Editor.Service.VerticalOffset, maximum - 1, maximum + 1);
        Assert.True(host.Bookmarks.NavigateNext(host.Editor));
        await Settle();
        Assert.InRange(host.Editor.Service.VerticalOffset, 0, 1);
        Assert.True(host.Bookmarks.NavigatePrevious(host.Editor));
        await Settle();
        Assert.InRange(host.Editor.Service.VerticalOffset, maximum - 1, maximum + 1);
    }

    [WpfFact]
    public async Task InternalHyperlink_CentersItsBookmarkDestination()
    {
        using var host = new EditorHost();
        var first = (Paragraph)host.Editor.Document.Blocks.FirstBlock!;
        var link = new Hyperlink(new Run("Go to middle")) {
            NavigateUri = host.Bookmarks.Bookmarks.Single(b => b.Name == "Middle").BookmarkUri
        };
        first.Inlines.Add(link);
        await Settle();
        var args = new RequestNavigateEventArgs(link.NavigateUri!, null) {
            RoutedEvent = Hyperlink.RequestNavigateEvent
        };
        link.RaiseEvent(args);
        Assert.True(args.Handled);
        await Settle();
        AssertCentered(host);
    }

    [WpfFact]
    public async Task LargeParagraph_CentersTheAnchorLine()
    {
        using var host = new EditorHost();
        host.Editor.Document.Blocks.Clear();
        var run = new Run(string.Join(" ", Enumerable.Repeat("long paragraph text", 800)));
        var paragraph = new Paragraph(run);
        host.Editor.Document.Blocks.Add(paragraph);
        host.Editor.CaretPosition = run.ContentStart.GetPositionAtOffset(run.Text.Length / 2)!;
        host.Bookmarks.RebuildIndexFromDocument(host.Editor);
        host.Bookmarks.AddAtCaret(host.Editor, "Middle");
        host.Editor.CaretPosition = paragraph.ContentStart;
        await Settle();
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        await Settle();
        AssertCentered(host);
    }

    [WpfFact]
    public async Task PendingNavigation_DoesNotScrollAReplacementDocument()
    {
        using var host = new EditorHost();
        await Settle();
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        host.Editor.ReplaceDocument(new FlowDocument(new Paragraph(new Run("New document"))));
        await Settle();
        Assert.Equal(0, host.Editor.Service.VerticalOffset);
        Assert.Contains("New document", new TextRange(host.Editor.Document.ContentStart, host.Editor.Document.ContentEnd).Text);
    }

    [WpfFact]
    public async Task RapidNavigation_KeepsTheLastDestination()
    {
        using var host = new EditorHost();
        await Settle();
        host.Bookmarks.NavigateTo(host.Editor, "Last");
        host.Bookmarks.NavigateTo(host.Editor, "First");
        host.Bookmarks.NavigateTo(host.Editor, "Middle");
        await Settle();
        AssertCentered(host);
    }

    [WpfFact]
    public async Task PendingNavigation_DoesNotOverrideANewerCaretMove()
    {
        using var host = new EditorHost();
        await Settle();
        host.Bookmarks.NavigateTo(host.Editor, "Middle");
        host.Editor.CaretPosition = host.Editor.Document.ContentStart;
        host.Editor.Service.ScrollToHome();
        await Settle();
        Assert.InRange(host.Editor.Service.VerticalOffset, 0, 1);
    }

    [WpfFact]
    public async Task Highlight_IsTemporaryAndDoesNotChangeTheDocument()
    {
        using var host = new EditorHost();
        var session = new DocumentSession(host.Editor, bookmarks: host.Bookmarks);
        session.Open("navigation.XamlPackage", new CryptoBook.FileTemplates.XamlPackageFileTemplate());
        string before = new TextRange(host.Editor.Document.ContentStart, host.Editor.Document.ContentEnd).Text;
        bool undoBefore = host.Editor.CanUndo;
        await Settle();
        host.Bookmarks.NavigateTo(host.Editor, "Middle");
        await Settle();
        var marker = Assert.Single(Markers(host));
        Assert.False(marker.IsHitTestVisible);
        Assert.True(marker.GetVisibleBounds().Width > host.Editor.Service.ViewportWidth / 2);
        Assert.False(session.IsDirty);
        Assert.Equal(undoBefore, host.Editor.CanUndo);
        Assert.Equal(before, new TextRange(host.Editor.Document.ContentStart, host.Editor.Document.ContentEnd).Text);
        host.Capture();
        await Task.Delay(3000);
        await Settle();
        Assert.Empty(Markers(host));
        host.Bookmarks.NavigateTo(host.Editor, "Middle");
        await Settle();
        Assert.Single(Markers(host));
        host.Editor.Selection.Text = "edit";
        await Settle();
        Assert.Empty(Markers(host));
        Assert.True(session.IsDirty);
    }

    [WpfFact]
    public async Task ExistingHighlight_IsRemovedWhenDocumentIsReplaced()
    {
        using var host = new EditorHost();
        await Settle();
        host.Bookmarks.NavigateTo(host.Editor, "Middle");
        await Settle();
        Assert.Single(Markers(host));
        host.Editor.ReplaceDocument(new FlowDocument(new Paragraph(new Run("Replacement"))));
        await Settle();
        Assert.Empty(Markers(host));
    }

    [WpfFact]
    public async Task Highlight_FollowsScrollingAndClearsOnCaretMovement()
    {
        using var host = new EditorHost();
        await Settle();
        host.Bookmarks.NavigateTo(host.Editor, "Middle");
        await Settle();
        var marker = Assert.Single(Markers(host));
        double top = marker.GetVisibleBounds().Top;
        host.Editor.Service.ScrollToVerticalOffset(host.Editor.Service.VerticalOffset + 40);
        await Settle();
        Assert.InRange(marker.GetVisibleBounds().Top, top - 41, top - 39);
        host.Editor.CaretPosition = host.Editor.Document.ContentStart;
        await Settle();
        Assert.Empty(Markers(host));
    }

    private static BookmarkNavigationAdorner[] Markers(EditorHost host) =>
        AdornerLayer.GetAdornerLayer(host.Editor.Service)?.GetAdorners(host.Editor.Service)?
            .OfType<BookmarkNavigationAdorner>().ToArray() ?? [];

    private static void AssertCentered(EditorHost host)
    {
        var bounds = host.Editor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
        Assert.False(bounds.IsEmpty);
        Assert.InRange(Math.Abs(bounds.Top + bounds.Height / 2 - host.Editor.Service.ViewportHeight / 2), 0, 3);
        var target = host.Bookmarks.Bookmarks.Single(b => b.Name == "Middle").BookmarkUri!.OriginalString.TrimStart('#');
        var anchor = new CryptoBook.Infrastructure.FlowDocumentWalker().Find<Span>(host.Editor.Document)
            .Single(s => s.Name == target);
        // Inline boundaries can normalize to the same visual insertion position.
        Assert.Empty(new TextRange(anchor.ContentStart, host.Editor.CaretPosition).Text);
        Assert.True(host.Editor.CaretPosition.CompareTo(anchor.ContentEnd) <= 0);
        var marker = Assert.Single(Markers(host));
        Assert.InRange(Math.Abs(marker.GetVisibleBounds().Top + marker.GetVisibleBounds().Height / 2 -
            host.Editor.Service.ViewportHeight / 2), 0, 3);
    }

    private static async Task Settle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private sealed class EditorHost : IDisposable
    {
        private readonly Window window;
        public IRichTextBoxService Editor { get; }
        public BookmarksService Bookmarks { get; }

        public EditorHost(double scale = 1)
        {
            Editor = new RichTextBoxService(new ParagraphFactory(), new TestUriNavigationService(), new DocumentAppearanceDefaults());
            Editor.Service.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            Editor.Service.LayoutTransform = new ScaleTransform(scale, scale);
            Editor.Document.Blocks.Clear();
            var paragraphs = Enumerable.Range(0, 180).Select(i => new Paragraph(new Run($"Paragraph {i}")) {
                Margin = new Thickness(0), LineHeight = 24
            }).ToArray();
            foreach(var paragraph in paragraphs) Editor.Document.Blocks.Add(paragraph);
            Bookmarks = new BookmarksService(Editor);
            foreach(var (name, index) in new[] { ("First", 0), ("Middle", 90), ("Last", 179) })
            {
                Editor.CaretPosition = paragraphs[index].ContentStart;
                Bookmarks.AddAtCaret(Editor, name);
            }
            Editor.CaretPosition = paragraphs[0].ContentStart;
            window = new Window { Content = Editor.Service, Width = 640, Height = 360,
                ShowInTaskbar = false, ShowActivated = false };
            window.Show();
            Editor.Service.UpdateLayout();
            Editor.Service.ScrollToHome();
        }
        public void Dispose() => window.Close();

        public void Capture()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while(root is not null && !Directory.Exists(Path.Combine(root.FullName, "CryptoBook", "Services")))
                root = root.Parent;
            Assert.NotNull(root);
            string directory = Path.Combine(root.FullName, "artifacts", "BookmarkNavigation");
            Directory.CreateDirectory(directory);
            var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            image.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(Path.Combine(directory, "target-highlight.png"));
            encoder.Save(stream);
        }
    }

    private sealed class ParagraphFactory : IParagraphFactory
    {
        public IParagraphService Create(Inline? inline = null)
        {
            var paragraph = new ParagraphService();
            if(inline is not null) paragraph.Inlines.Add(inline);
            return paragraph;
        }
    }
}

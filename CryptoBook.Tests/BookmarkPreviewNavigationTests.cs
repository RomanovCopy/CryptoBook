using Autofac;
using CryptoBook.Adorners;
using CryptoBook.Behaviors;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using Microsoft.Xaml.Behaviors;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Xunit;

namespace CryptoBook.Tests;

public sealed class BookmarkPreviewNavigationTests
{
    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkInsertion_IsDisabledInPreviewInPanelAndManager(bool manager)
    {
        using var host = new PreviewHost();
        await Settle();
        host.MenuViewModel.SelectedBookmark = host.Bookmarks.Bookmarks.Single(b => b.Name == "Middle");
        host.MenuViewModel.LinkText = "Draft link";
        object viewModel = manager ? host.ManagerViewModel : host.MenuViewModel;
        var command = manager ? host.ManagerViewModel.InsertHyperlinkTo : host.MenuViewModel.InsertHyperlinkTo;
        var linkText = new TextBox();
        linkText.SetBinding(UIElement.IsEnabledProperty,
            new Binding("IsLinkInsertionEnabled") { Source = viewModel });
        var button = new Button { Command = command };
        int linksBefore = new FlowDocumentWalker().Find<Hyperlink>(host.Editor.Document).Count();

        Assert.False(command.CanExecute(null));
        Assert.False(button.IsEnabled);
        Assert.False(linkText.IsEnabled);
        command.Execute(null);
        Assert.Equal(linksBefore, new FlowDocumentWalker().Find<Hyperlink>(host.Editor.Document).Count());
        host.AssertDocumentUnchanged();

        host.ViewModel.ToggleView.Execute(null);
        await Settle();
        Assert.True(button.IsEnabled);
        Assert.True(linkText.IsEnabled);
        Assert.Equal("Draft link", host.MenuViewModel.LinkText);
        command.Execute(null);
        Assert.Equal(linksBefore + 1, new FlowDocumentWalker().Find<Hyperlink>(host.Editor.Document).Count());
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletingBookmark_UnlinksTheSourceAndVisiblePreview(bool manager)
    {
        using var host = new PreviewHost();
        await Settle();
        host.MenuViewModel.SelectedBookmark = host.Bookmarks.Bookmarks.Single(b => b.Name == "Middle");
        var command = manager ? host.ManagerViewModel.Remove : host.MenuViewModel.Remove;
        Assert.True(command.CanExecute(null));
        command.Execute(null);
        await Settle();

        Assert.False(host.Bookmarks.Exists("Middle"));
        Assert.Empty(new FlowDocumentWalker().Find<Hyperlink>(host.Editor.Document));
        Assert.Empty(new FlowDocumentWalker().Find<Hyperlink>(host.ViewModel.PreviewDocument!));
        Assert.Contains("Go to middle", new TextRange(host.Editor.Document.ContentStart, host.Editor.Document.ContentEnd).Text);
        Assert.Contains("Go to middle", new TextRange(host.ViewModel.PreviewDocument!.ContentStart,
            host.ViewModel.PreviewDocument.ContentEnd).Text);
        Assert.True(host.ViewModel.IsPreviewMode);
        Assert.True(host.IsDirty);
    }

    [WpfFact]
    public async Task CreationCommand_IsDisabledInPreviewAndRestoredInEditorWithoutLosingDraft()
    {
        using var host = new PreviewHost();
        await Settle();
        var menu = host.MenuViewModel;
        menu.NewBookmarkName = "Draft bookmark";
        int count = menu.Bookmarks.Count;
        var nameBox = new TextBox();
        nameBox.SetBinding(UIElement.IsEnabledProperty, new Binding("IsBookmarkCreationEnabled") { Source = menu });
        var addButton = new Button { Command = menu.AddAtCaret };

        Assert.False(menu.AddAtCaret.CanExecute(null));
        Assert.False(nameBox.IsEnabled);
        Assert.False(addButton.IsEnabled);
        menu.AddAtCaret.Execute(null);
        Assert.Equal(count, menu.Bookmarks.Count);
        host.AssertDocumentUnchanged();

        int stateChanges = 0;
        menu.AddAtCaret.CanExecuteChanged += (_, _) => stateChanges++;
        host.ViewModel.ToggleView.Execute(null);
        Assert.True(stateChanges > 0);
        await Settle();
        Assert.Equal("Draft bookmark", menu.NewBookmarkName);
        Assert.True(nameBox.IsEnabled);
        Assert.True(addButton.IsEnabled);
        host.Editor.IsReadOnly = true;
        Assert.False(menu.AddAtCaret.CanExecute(null));
        host.Editor.IsReadOnly = false;
        menu.AddAtCaret.Execute(null);
        Assert.Equal(count + 1, menu.Bookmarks.Count);
        Assert.Equal("Draft bookmark", menu.SelectedBookmark!.Name);
    }

    [WpfFact]
    public async Task PreviewHyperlink_UpdatesTheSelectedBookmarkInTheMenu()
    {
        using var host = new PreviewHost();
        await Settle();
        var menu = host.MenuViewModel;
        menu.SelectedBookmark = null;
        var selected = new ComboBox { ItemsSource = menu.Bookmarks };
        selected.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
            new Binding(nameof(menu.SelectedBookmark)) { Source = menu, Mode = BindingMode.TwoWay });
        var navigateButton = new Button { Command = menu.NavigateTo };
        Assert.False(navigateButton.IsEnabled);
        var link = new FlowDocumentWalker().Find<Hyperlink>(host.ViewModel.PreviewDocument!).Single();

        link.RaiseEvent(new RequestNavigateEventArgs(link.NavigateUri!, null) {
            RoutedEvent = Hyperlink.RequestNavigateEvent
        });
        Assert.True(navigateButton.IsEnabled);
        await Settle();

        var destination = host.Bookmarks.Bookmarks.Single(b => b.Name == "Middle");
        Assert.Same(destination, menu.SelectedBookmark);
        Assert.Same(destination, selected.SelectedItem);
        Assert.Equal("Middle", menu.RenameTo);
        Assert.True(menu.NavigateTo.CanExecute(null));
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task EditorHyperlinkAndAdjacentNavigation_KeepMenuSelectionSynchronized()
    {
        using var host = new PreviewHost();
        host.ViewModel.ToggleView.Execute(null);
        await Settle();
        var menu = host.MenuViewModel;
        menu.SelectedBookmark = host.Bookmarks.Bookmarks.Single(b => b.Name == "First");
        var link = new FlowDocumentWalker().Find<Hyperlink>(host.Editor.Document).Single();
        link.RaiseEvent(new RequestNavigateEventArgs(link.NavigateUri!, null) {
            RoutedEvent = Hyperlink.RequestNavigateEvent
        });
        await Settle();
        Assert.Equal("Middle", menu.SelectedBookmark!.Name);
        Assert.Equal("Middle", menu.RenameTo);

        menu.NextBookmark.Execute(null);
        Assert.Equal("Last", menu.SelectedBookmark!.Name);
        menu.PreviousBookmark.Execute(null);
        Assert.Equal("Middle", menu.SelectedBookmark!.Name);
        menu.NavigateTo.Execute(host.Bookmarks.Bookmarks.Single(b => b.Name == "Last"));
        Assert.Equal("Last", menu.SelectedBookmark!.Name);

        foreach(var uri in new[] { new Uri("#Missing", UriKind.Relative), new Uri("https://example.com") })
        {
            link.RaiseEvent(new RequestNavigateEventArgs(uri, null) { RoutedEvent = Hyperlink.RequestNavigateEvent });
            Assert.Equal("Last", menu.SelectedBookmark!.Name);
        }
        host.AssertDocumentUnchanged();
    }

    [WpfTheory]
    [InlineData(75)]
    [InlineData(150)]
    public async Task MenuNavigation_ChangesVisiblePageWithoutEditingTheDocument(double zoom)
    {
        using var host = new PreviewHost(zoom);
        await Settle();
        int targetPage = host.PageOf("Middle");
        Assert.True(targetPage > 1);
        Assert.Equal(1, host.Viewer.MasterPageNumber);

        host.Menu.SelectedBookmark = host.Bookmarks.Bookmarks.Single(b => b.Name == "Middle");
        Assert.True(host.Menu.CanNavigateTo(null));
        host.Menu.NavigateTo(null);
        await Settle();

        Assert.Equal(targetPage, host.Viewer.MasterPageNumber);
        host.AssertHighlightAt("Middle");
        host.Capture($"target-highlight-{zoom}.png");
        Assert.True(host.ViewModel.IsPreviewMode);
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task InternalHyperlink_NavigatesThePreviewInsteadOfOpeningAnExternalUri()
    {
        using var host = new PreviewHost();
        await Settle();
        int targetPage = host.PageOf("Middle");
        var link = new FlowDocumentWalker().Find<Hyperlink>(host.ViewModel.PreviewDocument!).Single();
        var args = new RequestNavigateEventArgs(link.NavigateUri!, null) {
            RoutedEvent = Hyperlink.RequestNavigateEvent
        };

        link.RaiseEvent(args);
        await Settle();

        Assert.Equal(targetPage, host.Viewer.MasterPageNumber);
        host.AssertHighlightAt("Middle");
        Assert.True(args.Handled);
        Assert.Null(host.Navigator.LastOpenedUri);
        Assert.True(host.ViewModel.IsPreviewMode);
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task AfterInternalHyperlink_NextPreviousAndWrapFollowTheVisibleDestination()
    {
        using var host = new PreviewHost();
        await Settle();
        var link = new FlowDocumentWalker().Find<Hyperlink>(host.ViewModel.PreviewDocument!).Single();
        link.RaiseEvent(new RequestNavigateEventArgs(link.NavigateUri!, null) {
            RoutedEvent = Hyperlink.RequestNavigateEvent
        });
        await Settle();
        Assert.Equal(host.PageOf("Middle"), host.Viewer.MasterPageNumber);

        foreach(var (next, name) in new[] { (true, "Last"), (true, "First"), (false, "Last"), (false, "Middle") })
        {
            if(next) host.Menu.NavigateNext();
            else host.Menu.NavigatePrevious();
            await Settle();
            Assert.Equal(host.PageOf(name), host.Viewer.MasterPageNumber);
            Assert.Equal(name, host.MenuViewModel.SelectedBookmark!.Name);
        }
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task UnknownFragmentAndExternalLink_DoNotMoveThePreview()
    {
        using var host = new PreviewHost();
        await Settle();
        var link = new FlowDocumentWalker().Find<Hyperlink>(host.ViewModel.PreviewDocument!).Single();
        var missing = new Uri("#MissingBookmark", UriKind.Relative);
        link.RaiseEvent(new RequestNavigateEventArgs(missing, null) {
            RoutedEvent = Hyperlink.RequestNavigateEvent
        });
        await Settle();
        Assert.Equal(1, host.Viewer.MasterPageNumber);
        Assert.Null(host.Navigator.LastOpenedUri);

        var external = new Uri("https://example.com");
        var args = new RequestNavigateEventArgs(external, null) {
            RoutedEvent = Hyperlink.RequestNavigateEvent
        };
        link.RaiseEvent(args);
        Assert.True(args.Handled);
        Assert.Equal(external, host.Navigator.LastOpenedUri);
        Assert.Equal(1, host.Viewer.MasterPageNumber);
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task DetachedBehavior_StopsNavigationAndCanBeAttachedAgain()
    {
        using var host = new PreviewHost();
        await Settle();
        var behaviors = Interaction.GetBehaviors(host.Viewer);
        var behavior = Assert.Single(behaviors.OfType<BookmarkPreviewNavigationBehavior>());
        behaviors.Remove(behavior);
        host.Menu.SelectedBookmark = host.Bookmarks.Bookmarks.Single(b => b.Name == "Middle");
        host.Menu.NavigateTo(null);
        await Settle();
        Assert.Equal(1, host.Viewer.MasterPageNumber);

        behaviors.Add(behavior);
        host.Menu.NavigateTo(null);
        await Settle();
        Assert.Equal(host.PageOf("Middle"), host.Viewer.MasterPageNumber);
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task ModeSwitch_NavigatesTheNewPreviewAfterReturningFromEditing()
    {
        using var host = new PreviewHost();
        await Settle();
        var firstPreview = host.ViewModel.PreviewDocument;
        host.ViewModel.ToggleView.Execute(null);
        await Settle();
        Assert.False(host.Viewer.IsVisible);
        Assert.Null(host.Viewer.Document);
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Last"));

        host.ViewModel.ToggleView.Execute(null);
        await Settle();
        Assert.NotSame(firstPreview, host.Viewer.Document);
        Assert.True(host.Viewer.IsVisible);
        host.Menu.SelectedBookmark = host.Bookmarks.Bookmarks.Single(b => b.Name == "Middle");
        host.Menu.NavigateTo(null);
        await Settle();
        Assert.Equal(host.PageOf("Middle"), host.Viewer.MasterPageNumber);
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task Highlight_ExpiresWithoutChangingSourceOrPreviewFormatting()
    {
        using var host = new PreviewHost();
        await Settle();
        string before = XamlWriter.Save(host.ViewModel.PreviewDocument);
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        await Settle();
        host.AssertHighlightAt("Middle");
        Assert.Equal(before, XamlWriter.Save(host.ViewModel.PreviewDocument));
        host.AssertDocumentUnchanged();

        await Task.Delay(3000);
        await Settle();
        Assert.Empty(host.Markers());
        Assert.Equal(before, XamlWriter.Save(host.ViewModel.PreviewDocument));
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task RapidNavigation_HighlightsOnlyTheLastDestination()
    {
        using var host = new PreviewHost();
        await Settle();
        foreach(string name in new[] { "Last", "First", "Middle" })
            Assert.True(host.Bookmarks.NavigateTo(host.Editor, name));
        await Settle();
        Assert.Equal(host.PageOf("Middle"), host.Viewer.MasterPageNumber);
        host.AssertHighlightAt("Middle");
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task Highlight_ClearsOnPageChangeAndModeSwitch()
    {
        using var host = new PreviewHost();
        await Settle();
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        await Settle();
        host.AssertHighlightAt("Middle");
        host.Viewer.NextPage();
        await Settle();
        Assert.Empty(host.Markers());

        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        await Settle();
        host.AssertHighlightAt("Middle");
        host.ViewModel.ToggleView.Execute(null);
        await Settle();
        Assert.Empty(host.Markers());
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task PendingHighlight_DoesNotSurviveBehaviorDetachmentOrDocumentReplacement()
    {
        using var host = new PreviewHost();
        await Settle();
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        var behaviors = Interaction.GetBehaviors(host.Viewer);
        var behavior = Assert.Single(behaviors.OfType<BookmarkPreviewNavigationBehavior>());
        behaviors.Remove(behavior);
        await Settle();
        Assert.Empty(host.Markers());

        behaviors.Add(behavior);
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        host.Viewer.Document = new FlowDocument(new Paragraph(new Run("Replacement")));
        await Settle();
        Assert.Empty(host.Markers());
        host.AssertDocumentUnchanged();
    }

    [WpfFact]
    public async Task LongParagraph_HighlightsOnlyTheAnchorLine()
    {
        using var host = new PreviewHost();
        host.ViewModel.ToggleView.Execute(null);
        host.Editor.Document.Blocks.Clear();
        var run = new Run(string.Join(" ", Enumerable.Repeat("long paragraph text", 800)));
        host.Editor.Document.Blocks.Add(new Paragraph(run));
        host.Bookmarks.RebuildIndexFromDocument(host.Editor);
        host.Editor.CaretPosition = run.ContentStart.GetPositionAtOffset(run.Text.Length / 2)!;
        host.Bookmarks.AddAtCaret(host.Editor, "Middle");
        host.Editor.CaretPosition = host.Editor.Document.ContentStart;
        host.ViewModel.ToggleView.Execute(null);
        await Settle();

        Assert.True(host.PageOf("Middle") > 1);
        Assert.True(host.Bookmarks.NavigateTo(host.Editor, "Middle"));
        await Settle();
        host.AssertHighlightAt("Middle");
        Assert.True(Assert.Single(host.Markers()).GetVisibleBounds().Height < host.Viewer.ActualHeight / 4);
    }

    private static async Task Settle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private sealed class PreviewHost : IDisposable
    {
        private readonly IContainer container;
        private readonly Window window;
        private readonly DocumentSession session;
        private readonly string originalText;
        private readonly bool originalCanUndo;
        public IRichTextBoxService Editor { get; }
        public BookmarksService Bookmarks { get; }
        public BookmarksModel Menu { get; }
        public BookmarksViewModel MenuViewModel { get; }
        public BookmarksEditorViewModel ManagerViewModel { get; }
        public bool IsDirty => session.IsDirty;
        public RichtextboxViewModel ViewModel { get; }
        public FlowDocumentPageViewer Viewer { get; }
        public TestUriNavigationService Navigator { get; } = new();

        public PreviewHost(double zoom = 100)
        {
            Editor = new RichTextBoxService(new ParagraphFactory(), Navigator, new DocumentAppearanceDefaults());
            Editor.Document.Blocks.Clear();
            var paragraphs = Enumerable.Range(0, 180).Select(i => new Paragraph(new Run($"Paragraph {i}")) {
                Margin = new Thickness(0), LineHeight = 24
            }).ToArray();
            foreach(var paragraph in paragraphs)
                Editor.Document.Blocks.Add(paragraph);
            Bookmarks = new BookmarksService(Editor);
            foreach(var (name, index) in new[] { ("First", 0), ("Middle", 90), ("Last", 179) })
            {
                Editor.CaretPosition = paragraphs[index].ContentStart;
                Bookmarks.AddAtCaret(Editor, name);
            }
            Editor.CaretPosition = paragraphs[0].ContentEnd;
            Bookmarks.InsertHyperlinkTo(Editor, "Middle", "Go to middle");
            Editor.CaretPosition = paragraphs[0].ContentStart;
            Menu = new BookmarksModel(Editor, Bookmarks, new BookmarkValidationService(Bookmarks));
            ViewModel = new RichtextboxViewModel(new RichtextboxModel(), Editor,
                new DocumentPreviewService(), Navigator, new MenuFileViewModel());
            var builder = new ContainerBuilder();
            builder.RegisterInstance(Menu).As<IBookmarksModel>();
            builder.RegisterInstance(ViewModel).As<IRichtextboxViewModel>();
            builder.RegisterInstance(new CryptoBook.DTO.WindowContext(new Dictionary<string, object?> {
                [BookmarksEditorViewModel.BookmarksContextKey] = Menu,
                [BookmarksEditorViewModel.DocumentViewContextKey] = ViewModel
            })).As<IWindowContext>();
            builder.RegisterInstance(new WindowManager()).As<IWindowManager>();
            builder.RegisterType<BookmarksViewModel>();
            builder.RegisterType<BookmarksEditorModel>().As<IBookmarksEditorModel>();
            builder.RegisterType<BookmarksEditorViewModel>();
            container = builder.Build();
            MenuViewModel = container.Resolve<BookmarksViewModel>();
            ManagerViewModel = container.Resolve<BookmarksEditorViewModel>();
            session = new DocumentSession(Editor, bookmarks: Bookmarks);
            session.Open("preview.XamlPackage", new XamlPackageFileTemplate());
            originalText = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;
            originalCanUndo = Editor.CanUndo;
            ViewModel.ToggleView.Execute(null);
            Viewer = new FlowDocumentPageViewer { DataContext = ViewModel, Zoom = zoom };
            Viewer.SetBinding(FlowDocumentPageViewer.DocumentProperty, nameof(ViewModel.PreviewDocument));
            Viewer.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(ViewModel.IsPreviewMode)) {
                Converter = new BooleanToVisibilityConverter()
            });
            HyperlinkCommandBehavior.SetCommand(Viewer, ViewModel.OpenHyperlink);
            Interaction.GetBehaviors(Viewer).Add(new BookmarkPreviewNavigationBehavior { Bookmarks = Bookmarks });
            window = new Window { Content = Viewer, Width = 640, Height = 480,
                ShowInTaskbar = false, ShowActivated = false };
            window.Show();
            Viewer.UpdateLayout();
        }

        public int PageOf(string name)
        {
            var anchor = Anchor(name);
            var paginator = (DynamicDocumentPaginator)((IDocumentPaginatorSource)ViewModel.PreviewDocument!).DocumentPaginator;
            return paginator.GetPageNumber(anchor.ContentStart) + 1;
        }

        private Span Anchor(string name)
        {
            string anchorId = Bookmarks.Bookmarks.Single(b => b.Name == name).BookmarkUri!.OriginalString.TrimStart('#');
            return new FlowDocumentWalker().Find<Span>(ViewModel.PreviewDocument!).Single(s => s.Name == anchorId);
        }

        public BookmarkPreviewNavigationAdorner[] Markers()
        {
            var page = Viewer.PageViews.FirstOrDefault();
            if(page == null || AdornerLayer.GetAdornerLayer(page) is not { } layer ||
               VisualTreeHelper.GetParent(layer) is not AdornerDecorator { Child: { } scope })
                return [];
            return layer.GetAdorners(scope)?.OfType<BookmarkPreviewNavigationAdorner>().ToArray() ?? [];
        }

        public void AssertHighlightAt(string name)
        {
            var marker = Assert.Single(Markers());
            Assert.False(marker.IsHitTestVisible);
            var bounds = marker.GetVisibleBounds();
            Assert.False(bounds.IsEmpty);
            var caret = Anchor(name).ContentStart.GetInsertionPosition(LogicalDirection.Forward)
                .GetCharacterRect(LogicalDirection.Forward);
            Assert.True(bounds.Contains(new Point(caret.Left, caret.Top + caret.Height / 2)));
            Assert.InRange(Math.Abs(bounds.Top + bounds.Height / 2 - caret.Top - caret.Height / 2), 0, 1);
            Assert.InRange(bounds.Height, caret.Height, caret.Height + 6.1);
            Assert.True(bounds.Width > 100);
        }

        public void Capture(string name)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while(root != null && !Directory.Exists(Path.Combine(root.FullName, "CryptoBook", "Services")))
                root = root.Parent;
            Assert.NotNull(root);
            string directory = Path.Combine(root.FullName, "artifacts", "BookmarkPreviewNavigation");
            Directory.CreateDirectory(directory);
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, name));
            encoder.Save(stream);
        }

        public void AssertDocumentUnchanged()
        {
            Assert.False(session.IsDirty);
            Assert.Equal(originalCanUndo, Editor.CanUndo);
            Assert.Equal(originalText, new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text);
        }

        public void Dispose()
        {
            window.Close();
            ViewModel.Closed.Execute(null);
            container.Dispose();
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

    private sealed class MenuFileViewModel : IMenuFileViewModel
    {
        private static ICommand NoOp { get; } = new RelayCommand(_ => { });
        public ICommand NewFile => NoOp;
        public ICommand OpenFile => NoOp;
        public ICommand SaveFile => NoOp;
        public ICommand SaveAsFile => NoOp;
        public ICommand PrintFile => NoOp;
        public ICommand FileOverview => NoOp;
        public ICommand OpenDirectory => NoOp;
        public ICommand UpdateFile => NoOp;
        public ICommand CloseFile => NoOp;
        public ICommand WorkingDirectorySynchronization => NoOp;
    }

    private sealed class WindowManager : IWindowManager
    {
        public Guid CreateWindow<T>(IReadOnlyDictionary<string, object?>? args = null) where T : Window => Guid.NewGuid();
        public TResult? GetResult<TResult>(Guid guid) => default;
        public void ShowWindow(Guid windowId) { }
        public void ShowWindowDialog(Guid windowId) { }
        public void ActivateWindow(Guid windowId) { }
        public void CloseWindow(Guid windowId) { }
        public bool IsWindowOpen(Guid windowId) => false;
        public CryptoBook.DTO.WindowHost? FindHostWindow(Guid windowId) => null;
    }
}

using CryptoBook.Adorners;
using CryptoBook.Behaviors;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using Microsoft.Xaml.Behaviors;

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentSearchTests
{
    [Fact]
    public void TextSearch_IsLiteralAndSupportsCaseAndSpaces()
    {
        Assert.Equal(2, DocumentTextSearch.Find("Текст текст", "текст", false).Count);
        Assert.Single(DocumentTextSearch.Find("Текст текст", "текст", true));
        Assert.Single(DocumentTextSearch.Find("a.*b.*", ".*", false), match => match.Start == 1);
        Assert.Equal(2, DocumentTextSearch.Find("a.*b.*", ".*", false).Count);
        Assert.Single(DocumentTextSearch.Find("a  b", "  ", false));
        Assert.Empty(DocumentTextSearch.Find("text", "", false));
        Assert.Empty(DocumentTextSearch.Find("text", "longer text", false));
    }

    [WpfFact]
    public void RichSearch_FindsAcrossFormattingWithoutChangingDocument()
    {
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("Най"));
        paragraph.Inlines.Add(new Bold(new Run("ден")));
        paragraph.Inlines.Add(new Hyperlink(new Run("ный")));
        var document = new FlowDocument(paragraph);
        string before = XamlWriter.Save(document);

        var matches = DocumentTextSearch.Find(document, "найденный", false);

        Assert.Equal("Найденный", Assert.Single(matches).Text);
        Assert.Equal(before, XamlWriter.Save(document));
    }

    [WpfFact]
    public void RichSearch_DoesNotJoinParagraphsCellsOrEmbeddedObjects()
    {
        var paragraph = new Paragraph(new Run("first"));
        paragraph.Inlines.Add(new LineBreak());
        paragraph.Inlines.Add(new Run("second"));
        paragraph.Inlines.Add(new InlineUIContainer(new Border()));
        paragraph.Inlines.Add(new Run("third"));
        var document = new FlowDocument(paragraph);
        document.Blocks.Add(new Paragraph(new Run("fourth")));
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run("left"))));
        row.Cells.Add(new TableCell(new Paragraph(new Run("right"))));
        var group = new TableRowGroup();
        group.Rows.Add(row);
        var table = new Table();
        table.RowGroups.Add(group);
        document.Blocks.Add(table);

        Assert.Empty(DocumentTextSearch.Find(document, "firstsecond", false));
        Assert.Empty(DocumentTextSearch.Find(document, "secondthird", false));
        Assert.Empty(DocumentTextSearch.Find(document, "thirdfourth", false));
        Assert.Empty(DocumentTextSearch.Find(document, "leftright", false));
        Assert.Single(DocumentTextSearch.Find(document, "first\nsecond", false));
        Assert.Single(DocumentTextSearch.Find(document, "right", false));
    }

    [WpfFact]
    public void Navigation_WrapsInBothDirectionsAndDisablesEmptyResults()
    {
        var search = new DocumentSearchViewModel();
        Assert.False(search.Next.CanExecute(null));
        search.SetResultCount(3);
        search.Previous.Execute(null);
        Assert.Equal(2, search.CurrentIndex);
        search.Next.Execute(null);
        Assert.Equal(0, search.CurrentIndex);
        search.SetResultCount(0);
        Assert.Equal(-1, search.CurrentIndex);
        Assert.False(search.Previous.CanExecute(null));
    }

    [WpfFact]
    public void RichSearch_RendersAllMatchesAndLeavesSavedContentAndUndoUnchanged()
    {
        IRichTextBoxService service = new RichTextBoxService(new ParagraphFactoryStub(),
            new TestUriNavigationService(), new DocumentAppearanceDefaults());
        var session = new DocumentSession(service);
        session.Open(Path.Combine(Path.GetTempPath(), "search-test.rtf"),
            new RichTextFileTemplate(),
            new FlowDocument(new Paragraph(new Run("совпадение — совпадение"))));
        using var context = new SearchHost(service.Service);
        service.Service.IsUndoEnabled = false;
        service.Service.IsUndoEnabled = true;
        string before = XamlWriter.Save(service.Document);

        context.Find("совпадение");
        var adorner = context.GetAdorner();
        Assert.Equal(2, context.Search.ResultCount);
        Assert.Equal(new[] { 0, 1 }, adorner.GetVisibleRectangles().Select(item => item.Index).Distinct());
        Assert.True(CountHighlightPixels(context.Render("document-search-rich")) > 100);
        context.Search.Next.Execute(null);
        Assert.Equal("совпадение", service.Selection.Text);
        Assert.False(session.IsDirty);
        Assert.False(service.CanUndo);
        Assert.Equal(before, XamlWriter.Save(service.Document));

        context.Search.Close.Execute(null);
        Assert.Empty(context.GetAdorners());
        Assert.False(session.IsDirty);
    }

    [WpfFact]
    public void SourceSearch_RendersAllMatchesAndNavigatesWithoutEditingMarkdown()
    {
        const string source = "# Найти\n\n**найти** и [найти](path)";
        var editor = new TextBox { Text = source, AcceptsReturn = true, FontSize = 20 };
        using var context = new SearchHost(editor);
        editor.IsUndoEnabled = false;
        editor.IsUndoEnabled = true;
        int changes = 0;
        editor.TextChanged += (_, _) => changes++;

        context.Find("найти");

        Assert.Equal(3, context.Search.ResultCount);
        Assert.Equal(new[] { 0, 1, 2 }, context.GetAdorner().GetVisibleRectangles()
            .Select(item => item.Index).Distinct());
        Assert.True(CountHighlightPixels(context.Render("document-search-markdown")) > 100);
        context.Search.Next.Execute(null);
        Assert.Equal("найти", editor.SelectedText);
        Assert.Equal(source.IndexOf("найти", StringComparison.Ordinal), editor.SelectionStart);
        context.Search.MatchCase = true;
        context.Behavior.RefreshNow();
        Assert.Equal(2, context.Search.ResultCount);
        Assert.Equal(source, editor.Text);
        Assert.False(editor.CanUndo);
        Assert.Equal(0, changes);
    }

    [WpfFact]
    public void ChangingQueryOrDocument_ClearsStaleHighlightsAndRebuildsResults()
    {
        var editor = new RichTextBox(new FlowDocument(new Paragraph(new Run("one one"))));
        using var context = new SearchHost(editor);
        context.Find("one");
        Assert.Equal(2, context.Search.ResultCount);

        context.Search.Query = "missing";
        Assert.Empty(context.GetAdorners());
        Assert.False(context.Search.Next.CanExecute(null));
        context.Behavior.RefreshNow();
        Assert.Equal(0, context.Search.ResultCount);

        editor.Document = new FlowDocument(new Paragraph(new Run("missing missing missing")));
        context.Behavior.RefreshNow();
        Assert.Equal(3, context.Search.ResultCount);
        editor.Document.Blocks.Clear();
        context.Behavior.RefreshNow();
        Assert.Equal(0, context.Search.ResultCount);
        Assert.Empty(context.GetAdorners());
    }

    [WpfFact]
    public void SourceAndPreview_SearchOnlyTheVisibleEditor()
    {
        var source = new TextBox { Text = "**word** word" };
        var preview = new RichTextBox(new MarkdownFlowDocumentRenderer().Render("**word** word", null))
        {
            IsReadOnly = true,
            Visibility = Visibility.Collapsed
        };
        using var context = new SearchHost(source, preview);
        context.Find("**");
        Assert.Equal(2, context.Search.ResultCount);
        source.Visibility = Visibility.Collapsed;
        preview.Visibility = Visibility.Visible;
        context.Behavior.RefreshNow();
        Assert.Equal(0, context.Search.ResultCount);
        context.Find("word");
        Assert.Equal(2, context.Search.ResultCount);
        Assert.NotEmpty(context.GetAdorner(preview).GetVisibleRectangles());
        preview.Visibility = Visibility.Collapsed;
        source.Visibility = Visibility.Visible;
        context.Behavior.RefreshNow();
        Assert.Equal(2, context.Search.ResultCount);
        Assert.NotEmpty(context.GetAdorner().GetVisibleRectangles());
    }

    [WpfFact]
    public async Task OpeningSearch_FocusesQueryAndClosingReturnsFocusToEditor()
    {
        var editor = new TextBox { Text = "text text" };
        using var context = new SearchHost(editor);
        context.Find("text");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Assert.True(context.Query.IsKeyboardFocused);
        Assert.Equal(context.Query.Text, context.Query.SelectedText);
        context.Search.Close.Execute(null);
        Assert.True(editor.IsKeyboardFocused);
        Assert.Empty(context.GetAdorners());
    }

    [WpfFact]
    public void UnloadingSearch_RemovesHighlightsAndEventSubscriptions()
    {
        var editor = new TextBox { Text = "one one" };
        using var context = new SearchHost(editor);
        context.Find("one");
        Interaction.GetBehaviors(context.Root).Clear();
        Assert.Empty(context.GetAdorners());
        context.Search.Query = "two";
        editor.Text = "two";
        Assert.Equal(0, context.Search.ResultCount);
    }

    [WpfFact]
    public void Behavior_ResolvesEditorAndContentHostInTheViewsNamescope()
    {
        var editor = new TextBox { Text = "word word" };
        using var context = new SearchHost(editor);
        NameScope.SetNameScope(context.Root, new NameScope());
        context.Root.RegisterName("SourceEditor", editor);
        context.Behavior.EditorName = "SourceEditor";
        context.Behavior.Editor = null;
        Assert.Same(editor, context.Behavior.Editor);
        context.Find("word");
        Assert.Equal(2, context.Search.ResultCount);

        context.Root.RegisterName("EditorHost", new ContentControl { Content = new TextBox { Text = "new" } });
        context.Behavior.EditorName = "EditorHost";
        Assert.IsType<TextBox>(context.Behavior.Editor);
    }

    [WpfFact]
    public async Task QueryAndContentChanges_RefreshAutomatically()
    {
        var editor = new RichTextBox(new FlowDocument(new Paragraph(new Run("one one"))));
        using var context = new SearchHost(editor);
        context.Search.Open.Execute(null);
        context.Query.Text = "one";
        await Task.Delay(250);
        Assert.Equal(2, context.Search.ResultCount);
        editor.Document = new FlowDocument(new Paragraph(new Run("one")));
        await Task.Delay(250);
        Assert.Equal(1, context.Search.ResultCount);
        context.Query.Text = string.Empty;
        await Task.Delay(250);
        Assert.Equal(0, context.Search.ResultCount);
        Assert.Empty(context.GetAdorners());
    }

    [WpfFact]
    public void Navigation_ScrollsToMatchesOutsideViewport()
    {
        var document = new FlowDocument();
        document.Blocks.Add(new Paragraph(new Run("needle at start")));
        for(int index = 0; index < 100; index++)
            document.Blocks.Add(new Paragraph(new Run("filler line")));
        document.Blocks.Add(new Paragraph(new Run("needle at end")));
        var editor = new RichTextBox(document) { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        using var context = new SearchHost(editor);
        context.Find("needle");
        Assert.Equal(0, editor.VerticalOffset);
        context.Search.Next.Execute(null);
        editor.UpdateLayout();
        Assert.True(editor.VerticalOffset > editor.ViewportHeight);
        Assert.Equal(new[] { 1 }, context.GetAdorner().GetVisibleRectangles()
            .Select(item => item.Index).Distinct());
        context.Search.Next.Execute(null);
        editor.UpdateLayout();
        Assert.Equal(0, editor.VerticalOffset);
    }

    [WpfFact]
    public void ProductionSearchBar_BindsVisibilityQueryStatusAndCommands()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,
                  "CryptoBook", "MyControls", "DocumentSearchBar.xaml")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var xaml = XDocument.Load(Path.Combine(directory.FullName,
            "CryptoBook", "MyControls", "DocumentSearchBar.xaml"));
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace infra = "clr-namespace:CryptoBook.Infrastructure;assembly=CryptoBook";
        var root = xaml.Root!;
        root.Attribute(x + "Class")!.Remove();
        root.Add(new XAttribute(XNamespace.Xmlns + "infra", infra));
        root.AddFirst(new XElement(ns + "UserControl.Resources",
            new XElement(ns + "ResourceDictionary",
                new XElement(ns + "ResourceDictionary.MergedDictionaries",
                    new XElement(ns + "ResourceDictionary", new XAttribute("Source",
                        "/CryptoBook;component/Themes/LightTheme.xaml")),
                    new XElement(ns + "ResourceDictionary", new XAttribute("Source",
                        "/CryptoBook;component/Styles/ThemedControls.xaml"))),
                new XElement(infra + "ResourceWrapper", new XAttribute(x + "Key", "Res")))));
        var bar = Assert.IsType<UserControl>(XamlReader.Parse(root.ToString()));
        var editor = new TextBox { Text = "word word", FontSize = 20 };
        using var context = new SearchHost(editor, panel: bar);
        Assert.Equal(Visibility.Collapsed, bar.Visibility);
        context.Find("word");
        Assert.Equal(Visibility.Visible, bar.Visibility);
        Assert.Equal("word", context.Query.Text);
        var panel = Assert.IsType<WrapPanel>(Assert.IsType<Border>(bar.Content).Child);
        Assert.Contains(panel.Children.OfType<TextBlock>(), text => text.Text == context.Search.Status);
        var next = Assert.Single(panel.Children.OfType<Button>(), button => button.Content?.ToString() == "↓");
        next.Command.Execute(null);
        Assert.Equal(1, context.Search.CurrentIndex);
        context.Render("document-search-panel");
        context.Search.Close.Execute(null);
        Assert.Equal(Visibility.Collapsed, bar.Visibility);
    }

    private static int CountHighlightPixels(RenderTargetBitmap bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return Enumerable.Range(0, pixels.Length / 4).Count(index =>
            pixels[index * 4 + 2] > 200 && pixels[index * 4 + 1] > 150 &&
            pixels[index * 4] < 190);
    }

    private sealed class SearchHost: IDisposable
    {
        private readonly Window window;
        private readonly TextBoxBase editor;
        public DockPanel Root { get; } = new();
        public TextBox Query { get; }
        public DocumentSearchViewModel Search { get; } = new();
        public DocumentSearchBehavior Behavior { get; }

        public SearchHost(TextBoxBase editor, TextBoxBase? alternate = null, UserControl? panel = null)
        {
            this.editor = editor;
            Query = panel?.FindName("QueryInput") as TextBox ?? new TextBox();
            if(panel is null)
            {
                Query.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(Search.Query))
                {
                    Source = Search,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
                });
            }
            else
                panel.DataContext = Search;
            FrameworkElement searchPanel = panel is null ? Query : panel;
            DockPanel.SetDock(searchPanel, Dock.Top);
            Root.Children.Add(searchPanel);
            var grid = new Grid();
            grid.Children.Add(editor);
            if(alternate is not null)
                grid.Children.Add(alternate);
            Root.Children.Add(new AdornerDecorator { Child = grid });
            Behavior = new DocumentSearchBehavior
            {
                Search = Search, Editor = editor, AlternateEditor = alternate, SearchBox = Query
            };
            Interaction.GetBehaviors(Root).Add(Behavior);
            window = new Window
            {
                Content = Root, Width = 620, Height = 300, ShowInTaskbar = false,
                WindowStyle = WindowStyle.None, Background = Brushes.White
            };
            window.Show();
            Root.UpdateLayout();
        }

        public void Find(string query)
        {
            Search.Open.Execute(null);
            Search.Query = query;
            Root.UpdateLayout();
            Behavior.RefreshNow();
            Root.UpdateLayout();
        }

        public Adorner[] GetAdorners(TextBoxBase? target = null) =>
            AdornerLayer.GetAdornerLayer(target ?? editor)?.GetAdorners(target ?? editor) ?? [];

        public DocumentSearchAdorner GetAdorner(TextBoxBase? target = null) =>
            Assert.Single(GetAdorners(target).OfType<DocumentSearchAdorner>());

        public RenderTargetBitmap Render(string name)
        {
            Root.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Root.ActualWidth, (int)Root.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(Root);
            if(Environment.GetEnvironmentVariable("CRYPTOBOOK_UI_QA_DIR") is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(directory, name + ".png"));
                encoder.Save(stream);
            }
            return bitmap;
        }

        public void Dispose()
        {
            Interaction.GetBehaviors(Root).Clear();
            window.Close();
        }
    }

    private sealed class ParagraphFactoryStub: IParagraphFactory
    {
        public IParagraphService Create(Inline? inline = null)
        {
            var paragraph = new ParagraphService();
            if(inline is not null)
                paragraph.Inlines.Add(inline);
            return paragraph;
        }
    }
}

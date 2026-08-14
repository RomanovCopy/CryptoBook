using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentStructureTests
{
    [WpfFact]
    public void Startup_ResolvesDocumentStructureForHomeScope()
    {
        var app = Application.Current ?? new Application();
        using IContainer container = new Startup().ConfigureServices(app);
        using ILifetimeScope scope = container.BeginLifetimeScope();

        IDocumentStructureViewModel structure =
            scope.Resolve<IDocumentStructureViewModel>();
        IHomeViewModel home = scope.Resolve<IHomeViewModel>();

        Assert.Same(structure, home.DocumentStructure);
    }

    [WpfFact]
    public void CompactTree_ContainsStructureAndPromotesAnchoredBlocks()
    {
        var formatted = new Span(new Run("formatted"));
        var firstParagraph = new Paragraph(formatted);
        var list = new System.Windows.Documents.List(
            new ListItem(new Paragraph(new Run("item"))));
        var table = new Table();
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run("cell"))));
        group.Rows.Add(row);
        table.RowGroups.Add(group);

        var imageBlock = new BlockUIContainer(new Image());
        var figure = new Figure();
        figure.Blocks.Add(imageBlock);
        var figureParagraph = new Paragraph(figure);
        var document = new FlowDocument();
        document.Blocks.Add(firstParagraph);
        document.Blocks.Add(list);
        document.Blocks.Add(table);
        document.Blocks.Add(figureParagraph);

        DocumentStructureNode root =
            new FlowDocumentStructureBuilder().Build(
                document,
                includeTextElements: false);
        DocumentStructureNode[] nodes = Flatten(root).ToArray();

        Assert.Contains(nodes, node => node.Source is Paragraph);
        Assert.Contains(nodes, node => node.Source is ListItem);
        Assert.Contains(nodes, node => node.Source is TableCell);
        Assert.Contains(nodes, node => ReferenceEquals(node.Source, figure));
        Assert.Contains(nodes, node => ReferenceEquals(node.Source, imageBlock));
        Assert.DoesNotContain(nodes, node => node.Source is Run);
        Assert.DoesNotContain(nodes, node => ReferenceEquals(node.Source, formatted));
    }

    [WpfFact]
    public void DetailedTree_HidesCaretMarkersAndProtectsBookmarkAnchor()
    {
        var bookmark = new Span
        {
            Name = $"Bookmark_{Guid.NewGuid():N}",
            Tag = "CryptoBook.Bookmark:{}"
        };
        bookmark.Inlines.Add(new Run("\u200B"));
        var formatting = new Span(new Run("visible"));
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(bookmark);
        paragraph.Inlines.Add(formatting);
        var document = new FlowDocument(paragraph);

        DocumentStructureNode root =
            new FlowDocumentStructureBuilder().Build(
                document,
                includeTextElements: true);
        DocumentStructureNode[] nodes = Flatten(root).ToArray();

        DocumentStructureNode bookmarkNode = Assert.Single(
            nodes,
            node => ReferenceEquals(node.Source, bookmark));
        Assert.False(bookmarkNode.CanDelete);
        Assert.Contains("bookmark", bookmarkNode.Summary);
        Assert.Contains(nodes, node => ReferenceEquals(node.Source, formatting));
        Assert.Contains(nodes, node =>
            node.Source is Run { Text: "visible" });
        Assert.DoesNotContain(nodes, node =>
            node.Source is Run { Text: "\u200B" });
    }

    [WpfFact]
    public void Walker_TraversesBlocksInsideFigure()
    {
        var nestedParagraph = new Paragraph(new Run("inside"));
        var figure = new Figure();
        figure.Blocks.Add(nestedParagraph);
        var document = new FlowDocument(new Paragraph(figure));

        TextElement[] elements =
            new FlowDocumentWalker().Traverse(document).ToArray();

        Assert.Contains(figure, elements);
        Assert.Contains(nestedParagraph, elements);
        Assert.Contains(
            elements,
            element => element is Run { Text: "inside" });
    }

    [WpfFact]
    public async Task DeleteCommand_RemovesNodeAndKeepsDocumentEditable()
    {
        TestContext context = CreateContext();
        var first = new Paragraph(new Run("first"));
        var second = new Paragraph(new Run("second"));
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(first);
        context.RichTextBox.Document.Blocks.Add(second);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode secondNode = Assert.Single(
            Flatten(Assert.Single(context.ViewModel.Nodes)),
            node => ReferenceEquals(node.Source, second));
        await ((IAsyncCommand)context.ViewModel.DeleteCommand)
            .ExecuteAsync(secondNode);

        Assert.Single(context.RichTextBox.Document.Blocks);
        Assert.Same(
            first,
            context.RichTextBox.Document.Blocks.FirstBlock);
        Assert.True(context.DocumentSession.IsDirty);

        DocumentStructureNode firstNode = Assert.Single(
            Flatten(Assert.Single(context.ViewModel.Nodes)),
            node => ReferenceEquals(node.Source, first));
        await ((IAsyncCommand)context.ViewModel.DeleteCommand)
            .ExecuteAsync(firstNode);

        Paragraph replacement = Assert.IsAssignableFrom<Paragraph>(
            Assert.Single(context.RichTextBox.Document.Blocks));
        Assert.NotSame(first, replacement);
        Assert.NotNull(context.RichTextBox.CaretPosition.Paragraph);
    }

    [WpfFact]
    public async Task DeletingBookmarkParent_RebuildsBookmarkIndex()
    {
        TestContext context = CreateContext();
        var paragraph = new Paragraph(new Run("bookmark target"));
        var survivor = new Paragraph(new Run("survivor"));
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(paragraph);
        context.RichTextBox.Document.Blocks.Add(survivor);
        context.RichTextBox.CaretPosition = paragraph.ContentStart;
        context.Bookmarks.AddAtCaret(context.RichTextBox, "Target");
        context.ViewModel.ToggleCommand.Execute(null);

        Assert.Single(context.Bookmarks.Bookmarks);
        DocumentStructureNode paragraphNode = Assert.Single(
            Flatten(Assert.Single(context.ViewModel.Nodes)),
            node => ReferenceEquals(node.Source, paragraph));

        await ((IAsyncCommand)context.ViewModel.DeleteCommand)
            .ExecuteAsync(paragraphNode);

        Assert.Empty(context.Bookmarks.Bookmarks);
        Assert.Same(
            survivor,
            context.RichTextBox.Document.Blocks.FirstBlock);
    }

    [WpfFact]
    public async Task DeletingLastListItem_RemovesEmptyListAndAddsParagraph()
    {
        TestContext context = CreateContext();
        var item = new ListItem(new Paragraph(new Run("only item")));
        var list = new System.Windows.Documents.List(item);
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(list);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode itemNode = Assert.Single(
            Flatten(Assert.Single(context.ViewModel.Nodes)),
            node => ReferenceEquals(node.Source, item));
        await ((IAsyncCommand)context.ViewModel.DeleteCommand)
            .ExecuteAsync(itemNode);

        Assert.IsAssignableFrom<Paragraph>(
            Assert.Single(context.RichTextBox.Document.Blocks));
        Assert.DoesNotContain(
            context.RichTextBox.Document.Blocks,
            block => block is System.Windows.Documents.List);
    }

    private static IEnumerable<DocumentStructureNode> Flatten(
        DocumentStructureNode root)
    {
        yield return root;
        foreach(DocumentStructureNode child in root.Children)
        {
            foreach(DocumentStructureNode descendant in Flatten(child))
                yield return descendant;
        }
    }

    private static TestContext CreateContext()
    {
        var paragraphFactory = new TestParagraphFactory();
        IRichTextBoxService richTextBox = new RichTextBoxService(
            paragraphFactory,
            new UriNavigationServiceStub(),
            new DocumentAppearanceDefaults());
        var documentSession = new DocumentSession(richTextBox);
        documentSession.SetDisplayName("Untitled.cbook");
        var bookmarks = new BookmarksService(richTextBox);
        var documentView = new DocumentViewStub();
        var viewModel = new DocumentStructureViewModel(
            new FlowDocumentStructureBuilder(),
            new FlowDocumentWalker(),
            richTextBox,
            documentView,
            new EmbeddedImageLayoutService(documentSession),
            bookmarks,
            documentSession,
            paragraphFactory,
            new ConfirmationMessageService());

        return new TestContext(
            richTextBox,
            documentSession,
            bookmarks,
            viewModel);
    }

    private sealed record TestContext(
        IRichTextBoxService RichTextBox,
        DocumentSession DocumentSession,
        BookmarksService Bookmarks,
        DocumentStructureViewModel ViewModel);

    private sealed class TestParagraphFactory: IParagraphFactory
    {
        public IParagraphService Create(Inline? inline = null)
        {
            var paragraph = new ParagraphService();
            if(inline is not null)
                paragraph.Inlines.Add(inline);
            return paragraph;
        }
    }

    private sealed class ConfirmationMessageService: IMessageService
    {
        public Task<Guid> ShowMessage(
            string title,
            string message,
            bool isCanceled = false) =>
            Task.FromResult(Guid.NewGuid());

        public void CloseDialog(Guid id)
        {
        }

        public bool ShowConfirmation(Guid id) => true;
    }

    private sealed class UriNavigationServiceStub: IUriNavigationService
    {
        public bool TryOpen(Uri uri) => true;
    }

    private sealed class DocumentViewStub:
        ViewModelBase,
        IRichtextboxViewModel
    {
        private static ICommand NoOp { get; } = new RelayCommand(_ => { });

        public bool IsPreviewMode { get; private set; }
        public bool IsFitToWindow => false;
        public string ModeLabel => string.Empty;
        public string ToggleViewText => string.Empty;
        public string FitToWindowText => string.Empty;
        public string FitToWindowGlyph => string.Empty;
        public FlowDocument? PreviewDocument => null;
        public ICommand ToggleView => NoOp;
        public ICommand ToggleFitToWindow => NoOp;
        public ICommand OpenHyperlink => NoOp;
        public ICommand SaveDocument => NoOp;
        public ICommand SaveDocumentAs => NoOp;
        public ICommand Loaded => NoOp;
        public ICommand Close => NoOp;
        public ICommand Closing => NoOp;
        public ICommand Closed => NoOp;
    }
}

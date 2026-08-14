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

    [WpfFact]
    public void AddParagraphBeforeAndAfter_InsertsSelectsAndFocusesNewParagraph()
    {
        TestContext context = CreateContext();
        var first = new Paragraph(new Run("first"));
        var second = new Paragraph(new Run("second"));
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(first);
        context.RichTextBox.Document.Blocks.Add(second);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode firstNode = FindNode(
            Assert.Single(context.ViewModel.Nodes),
            first);
        context.ViewModel.AddParagraphBeforeCommand.Execute(firstNode);

        Paragraph insertedBefore = Assert.IsAssignableFrom<Paragraph>(
            context.RichTextBox.Document.Blocks.FirstBlock);
        Assert.NotSame(first, insertedBefore);
        Assert.Same(insertedBefore, context.RichTextBox.CaretPosition.Paragraph);
        Assert.True(context.DocumentSession.IsDirty);
        DocumentStructureNode selectedBefore = Assert.Single(
            Flatten(Assert.Single(context.ViewModel.Nodes)),
            node => node.IsSelected);
        Assert.Same(insertedBefore, selectedBefore.Source);
        Assert.StartsWith("Paragraph 1", selectedBefore.DisplayName);

        firstNode = FindNode(
            Assert.Single(context.ViewModel.Nodes),
            first);
        context.ViewModel.AddParagraphAfterCommand.Execute(firstNode);

        Block[] blocks = context.RichTextBox.Document.Blocks.Cast<Block>().ToArray();
        Assert.Equal(4, blocks.Length);
        Assert.Same(insertedBefore, blocks[0]);
        Assert.Same(first, blocks[1]);
        Paragraph insertedAfter = Assert.IsAssignableFrom<Paragraph>(blocks[2]);
        Assert.Same(second, blocks[3]);
        Assert.Same(insertedAfter, context.RichTextBox.CaretPosition.Paragraph);
        Assert.Same(
            insertedAfter,
            Assert.Single(
                Flatten(Assert.Single(context.ViewModel.Nodes)),
                node => node.IsSelected).Source);
    }

    [WpfFact]
    public void AddParagraphInside_AppendsToEverySupportedOwnerAndExpandsSelection()
    {
        TestContext context = CreateContext();
        var section = new Section(new Paragraph(new Run("section")));
        var item = new ListItem(new Paragraph(new Run("item")));
        var list = new System.Windows.Documents.List(item);
        var cell = new TableCell(new Paragraph(new Run("cell")));
        var row = new TableRow();
        row.Cells.Add(cell);
        var group = new TableRowGroup();
        group.Rows.Add(row);
        var table = new Table();
        table.RowGroups.Add(group);
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(section);
        context.RichTextBox.Document.Blocks.Add(list);
        context.RichTextBox.Document.Blocks.Add(table);
        context.ViewModel.ToggleCommand.Execute(null);

        ExecuteAddInside(context, context.RichTextBox.Document);
        Assert.IsAssignableFrom<Paragraph>(
            context.RichTextBox.Document.Blocks.LastBlock);

        ExecuteAddInside(context, section);
        Assert.Equal(2, section.Blocks.Count);

        ExecuteAddInside(context, item);
        Assert.Equal(2, item.Blocks.Count);

        ExecuteAddInside(context, cell);
        Assert.Equal(2, cell.Blocks.Count);
        Paragraph inserted = Assert.IsAssignableFrom<Paragraph>(
            cell.Blocks.LastBlock);
        DocumentStructureNode root = Assert.Single(context.ViewModel.Nodes);
        DocumentStructureNode selected = Assert.Single(
            Flatten(root),
            node => node.IsSelected);
        Assert.Same(inserted, selected.Source);
        Assert.True(root.IsExpanded);
        Assert.True(FindNode(root, table).IsExpanded);
        Assert.True(FindNode(root, row).IsExpanded);
        Assert.True(FindNode(root, cell).IsExpanded);
        Assert.Same(inserted, context.RichTextBox.CaretPosition.Paragraph);
    }

    [WpfFact]
    public void AddParagraphCommand_UsesContainingBlockAndFallsBackToDocument()
    {
        TestContext context = CreateContext();
        var run = new Run("first");
        var first = new Paragraph(run);
        var second = new Paragraph(new Run("second"));
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(first);
        context.RichTextBox.Document.Blocks.Add(second);
        context.ViewModel.IncludeTextElements = true;
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode runNode = FindNode(
            Assert.Single(context.ViewModel.Nodes),
            run);
        context.ViewModel.AddParagraphCommand.Execute(runNode);

        Block[] blocks = context.RichTextBox.Document.Blocks.Cast<Block>().ToArray();
        Assert.Equal(3, blocks.Length);
        Assert.Same(first, blocks[0]);
        Paragraph insertedAfterFirst = Assert.IsAssignableFrom<Paragraph>(blocks[1]);
        Assert.Same(second, blocks[2]);
        Assert.Same(
            insertedAfterFirst,
            context.RichTextBox.CaretPosition.Paragraph);

        context.ViewModel.AddParagraphCommand.Execute(null);

        Assert.Equal(4, context.RichTextBox.Document.Blocks.Count);
        Assert.Same(
            context.RichTextBox.Document.Blocks.LastBlock,
            context.RichTextBox.CaretPosition.Paragraph);
    }

    [WpfFact]
    public void MoveCommand_MovesBlockIntoSectionAndPreservesSelection()
    {
        TestContext context = CreateContext();
        var paragraph = new Paragraph(new Run("move me"));
        var section = new Section(new Paragraph(new Run("target")));
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(paragraph);
        context.RichTextBox.Document.Blocks.Add(section);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode root = Assert.Single(context.ViewModel.Nodes);
        DocumentStructureNode source = Assert.Single(
            Flatten(root),
            node => ReferenceEquals(node.Source, paragraph));
        DocumentStructureNode target = Assert.Single(
            Flatten(root),
            node => ReferenceEquals(node.Source, section));
        long revisionBefore = context.DocumentSession.Revision;
        var request = new DocumentStructureMoveRequest(
            source,
            target,
            DocumentStructureDropPosition.Inside);

        Assert.True(context.ViewModel.MoveCommand.CanExecute(request));
        context.ViewModel.MoveCommand.Execute(request);

        Assert.Same(section, context.RichTextBox.Document.Blocks.FirstBlock);
        Assert.Same(paragraph, section.Blocks.LastBlock);
        Assert.Equal(revisionBefore + 1, context.DocumentSession.Revision);
        DocumentStructureNode refreshedRoot = Assert.Single(
            context.ViewModel.Nodes);
        Assert.True(Assert.Single(
            Flatten(refreshedRoot),
            node => ReferenceEquals(node.Source, section)).IsExpanded);
        Assert.True(Assert.Single(
            Flatten(refreshedRoot),
            node => ReferenceEquals(node.Source, paragraph)).IsSelected);
    }

    [WpfFact]
    public void MoveCommand_RejectsCyclesAndNoOpDrops()
    {
        TestContext context = CreateContext();
        var child = new Section(new Paragraph(new Run("child")));
        var parent = new Section(child);
        var sibling = new Paragraph(new Run("sibling"));
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(parent);
        context.RichTextBox.Document.Blocks.Add(sibling);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode root = Assert.Single(context.ViewModel.Nodes);
        DocumentStructureNode parentNode = FindNode(root, parent);
        DocumentStructureNode childNode = FindNode(root, child);
        DocumentStructureNode siblingNode = FindNode(root, sibling);

        Assert.False(context.ViewModel.MoveCommand.CanExecute(
            new DocumentStructureMoveRequest(
                parentNode,
                childNode,
                DocumentStructureDropPosition.Inside)));
        Assert.False(context.ViewModel.MoveCommand.CanExecute(
            new DocumentStructureMoveRequest(
                parentNode,
                siblingNode,
                DocumentStructureDropPosition.Before)));
    }

    [WpfFact]
    public void MoveCommand_MovesLastListItemAndRemovesEmptyList()
    {
        TestContext context = CreateContext();
        var sourceItem = new ListItem(new Paragraph(new Run("source")));
        var sourceList = new System.Windows.Documents.List(sourceItem);
        var targetItem = new ListItem(new Paragraph(new Run("target")));
        var targetList = new System.Windows.Documents.List(targetItem);
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(sourceList);
        context.RichTextBox.Document.Blocks.Add(targetList);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode root = Assert.Single(context.ViewModel.Nodes);
        var request = new DocumentStructureMoveRequest(
            FindNode(root, sourceItem),
            FindNode(root, targetList),
            DocumentStructureDropPosition.Inside);

        context.ViewModel.MoveCommand.Execute(request);

        Assert.Single(context.RichTextBox.Document.Blocks);
        Assert.Same(targetList, context.RichTextBox.Document.Blocks.FirstBlock);
        Assert.Equal(2, targetList.ListItems.Count);
        Assert.Same(sourceItem, targetList.ListItems.LastListItem);
    }

    [WpfFact]
    public void MoveCommand_PreservesExpansionByElementInsteadOfOldPath()
    {
        TestContext context = CreateContext();
        var first = new Section(new Paragraph(new Run("first")));
        var second = new Section(new Paragraph(new Run("second")));
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(first);
        context.RichTextBox.Document.Blocks.Add(second);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode root = Assert.Single(context.ViewModel.Nodes);
        DocumentStructureNode firstNode = FindNode(root, first);
        DocumentStructureNode secondNode = FindNode(root, second);
        firstNode.IsExpanded = true;
        secondNode.IsExpanded = false;

        context.ViewModel.MoveCommand.Execute(
            new DocumentStructureMoveRequest(
                firstNode,
                secondNode,
                DocumentStructureDropPosition.After));

        DocumentStructureNode refreshed = Assert.Single(
            context.ViewModel.Nodes);
        Assert.True(FindNode(refreshed, first).IsExpanded);
        Assert.False(FindNode(refreshed, second).IsExpanded);
    }

    [WpfFact]
    public void MoveUpAndDownCommands_ReorderOnlyCompatibleSiblings()
    {
        TestContext context = CreateContext();
        var first = new Paragraph(new Run("first"));
        var second = new Paragraph(new Run("second"));
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(first);
        context.RichTextBox.Document.Blocks.Add(second);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode root = Assert.Single(context.ViewModel.Nodes);
        DocumentStructureNode firstNode = FindNode(root, first);
        Assert.False(context.ViewModel.MoveUpCommand.CanExecute(firstNode));
        Assert.True(context.ViewModel.MoveDownCommand.CanExecute(firstNode));

        context.ViewModel.MoveDownCommand.Execute(firstNode);

        Assert.Same(second, context.RichTextBox.Document.Blocks.FirstBlock);
        DocumentStructureNode refreshed = Assert.Single(
            context.ViewModel.Nodes);
        DocumentStructureNode movedNode = FindNode(refreshed, first);
        Assert.True(context.ViewModel.MoveUpCommand.CanExecute(movedNode));
        Assert.False(context.ViewModel.MoveDownCommand.CanExecute(movedNode));

        context.RichTextBox.IsReadOnly = true;
        Assert.False(context.ViewModel.MoveUpCommand.CanExecute(movedNode));
    }

    [WpfFact]
    public void MoveCommand_ReordersTableRows()
    {
        TestContext context = CreateContext();
        var first = new TableRow();
        first.Cells.Add(new TableCell(new Paragraph(new Run("first"))));
        var second = new TableRow();
        second.Cells.Add(new TableCell(new Paragraph(new Run("second"))));
        var group = new TableRowGroup();
        group.Rows.Add(first);
        group.Rows.Add(second);
        var table = new Table();
        table.RowGroups.Add(group);
        context.RichTextBox.Document.Blocks.Clear();
        context.RichTextBox.Document.Blocks.Add(table);
        context.ViewModel.ToggleCommand.Execute(null);

        DocumentStructureNode root = Assert.Single(context.ViewModel.Nodes);
        var request = new DocumentStructureMoveRequest(
            FindNode(root, second),
            FindNode(root, first),
            DocumentStructureDropPosition.Before);

        context.ViewModel.MoveCommand.Execute(request);

        Assert.Same(second, group.Rows[0]);
        Assert.Same(first, group.Rows[1]);
    }

    [WpfFact]
    public void MoveCommand_UndoAndRedoRestoreOrderAsOneUnit()
    {
        TestContext context = CreateContext();
        var host = new Window
        {
            Content = context.RichTextBox.Service,
            Width = 320,
            Height = 200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };
        host.Show();
        try
        {
            var first = new Paragraph(new Run("first"));
            var second = new Paragraph(new Run("second"));
            context.RichTextBox.Document.Blocks.Clear();
            context.RichTextBox.Document.Blocks.Add(first);
            context.RichTextBox.Document.Blocks.Add(second);
            context.ViewModel.ToggleCommand.Execute(null);
            context.RichTextBox.Service.IsUndoEnabled = false;
            context.RichTextBox.Service.IsUndoEnabled = true;

            DocumentStructureNode root = Assert.Single(
                context.ViewModel.Nodes);
            var request = new DocumentStructureMoveRequest(
                FindNode(root, second),
                FindNode(root, first),
                DocumentStructureDropPosition.Before);
            context.ViewModel.MoveCommand.Execute(request);

            Assert.Same(
                second,
                context.RichTextBox.Document.Blocks.FirstBlock);
            Assert.True(context.RichTextBox.CanUndo);

            context.RichTextBox.Undo();

            Assert.Equal(
                "first",
                GetBlockText(context.RichTextBox.Document.Blocks.FirstBlock!));
            Assert.False(context.RichTextBox.CanUndo);
            Assert.True(context.RichTextBox.CanRedo);

            context.RichTextBox.Redo();

            Assert.Equal(
                "second",
                GetBlockText(context.RichTextBox.Document.Blocks.FirstBlock!));
        }
        finally
        {
            host.Close();
        }
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

    private static DocumentStructureNode FindNode(
        DocumentStructureNode root,
        FrameworkContentElement source) =>
        Assert.Single(
            Flatten(root),
            node => ReferenceEquals(node.Source, source));

    private static string GetBlockText(Block block) =>
        new TextRange(block.ContentStart, block.ContentEnd).Text.Trim();

    private static void ExecuteAddInside(
        TestContext context,
        FrameworkContentElement source)
    {
        DocumentStructureNode node = FindNode(
            Assert.Single(context.ViewModel.Nodes),
            source);
        Assert.True(context.ViewModel.AddParagraphInsideCommand.CanExecute(node));
        context.ViewModel.AddParagraphInsideCommand.Execute(node);
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
        var walker = new FlowDocumentWalker();
        var viewModel = new DocumentStructureViewModel(
            new FlowDocumentStructureBuilder(),
            walker,
            new FlowDocumentContentService(walker, paragraphFactory),
            new FlowDocumentMoveService(paragraphFactory),
            richTextBox,
            documentView,
            new EmbeddedImageLayoutService(documentSession),
            bookmarks,
            documentSession,
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

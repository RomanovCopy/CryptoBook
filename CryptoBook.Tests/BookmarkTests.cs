using CryptoBook.Interfaces;
using CryptoBook.Infrastructure;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using CryptoBook.Accessors;
using CryptoBook.Models;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Xunit;

namespace CryptoBook.Tests;

public sealed class BookmarkTests
{
    [WpfTheory]
    [InlineData("ab", 0)]
    [InlineData("ab", 1)]
    [InlineData("ab", 2)]
    [InlineData("", 0)]
    public void AddAtCaret_InsertsOwnTextWithoutIncludingSurroundingText(string text, int offset)
    {
        var editor = CreateRichText(text);
        var bookmarks = new BookmarksService(editor);
        var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock!;
        var run = (Run)paragraph.Inlines.FirstInline!;
        editor.CaretPosition = run.ContentStart.GetPositionAtOffset(offset)!;

        bookmarks.AddAtCaret(editor, "  Marker  ");

        var entry = Assert.Single(bookmarks.Bookmarks);
        var anchor = FindAnchor(editor.Document, entry.BookmarkUri!.OriginalString.TrimStart('#'))!;
        Assert.Equal("Marker", new TextRange(anchor.ContentStart, anchor.ContentEnd).Text);
        Assert.Equal(text.Insert(offset, "Marker"), new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
        Assert.True(editor.Selection.IsEmpty);
        editor.InsertTextAtCaret("!");
        Assert.Equal("Marker", new TextRange(anchor.ContentStart, anchor.ContentEnd).Text);
        Assert.True(bookmarks.Remove(editor, entry.Name));
        Assert.Equal(text.Insert(offset, "!"), new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddAtCaret_RejectsSelectionWithoutChangingTextOrSelection(bool multipleParagraphs)
    {
        var editor = CreateRichText("selected text", "second paragraph");
        var bookmarks = new BookmarksService(editor);
        var validation = new BookmarkValidationService(bookmarks);
        using var model = new BookmarksModel(editor, bookmarks, validation);
        using var viewModel = new BookmarksViewModel(model, new StubWindowManager());
        var paragraphs = editor.Document.Blocks.Cast<Paragraph>().ToArray();
        editor.Selection.Select(paragraphs[0].ContentStart,
            paragraphs[multipleParagraphs ? 1 : 0].ContentEnd);
        string before = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
        string selected = editor.Selection.Text;
        viewModel.NewBookmarkName = "Marker";

        Assert.False(validation.CanInsertBookmark(editor, "Marker", bookmarks.Exists).Ok);
        Assert.False(viewModel.AddAtCaret.CanExecute(null));
        viewModel.AddAtCaret.Execute(null);
        Assert.Throws<InvalidOperationException>(() => bookmarks.AddAtCaret(editor, "Marker"));

        Assert.Empty(bookmarks.Bookmarks);
        Assert.Equal(before, new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
        Assert.Equal(selected, editor.Selection.Text);
        editor.ClearSelection();
        Assert.True(viewModel.AddAtCaret.CanExecute(null));
        viewModel.AddAtCaret.Execute(null);
        Assert.Single(bookmarks.Bookmarks);
    }

    [WpfFact]
    public void Remove_DeletesOwnTextAndAllLinkCaptionsAndPreservesOtherLinksWithUndo()
    {
        var editor = CreateRichText("target", "other", "links");
        var bookmarks = new BookmarksService(editor);
        var paragraphs = editor.Document.Blocks.Cast<Paragraph>().ToArray();
        editor.CaretPosition = paragraphs[0].ContentStart;
        bookmarks.AddAtCaret(editor, "Target");
        var target = bookmarks.Bookmarks[0].BookmarkUri!;
        editor.CaretPosition = paragraphs[1].ContentStart;
        bookmarks.AddAtCaret(editor, "Other");
        var other = bookmarks.Bookmarks[1].BookmarkUri!;

        var formatted = new Hyperlink(new Bold(new Run("first caption"))) { NavigateUri = target, FontSize = 21 };
        paragraphs[2].Inlines.Add(new Span(formatted));
        paragraphs[2].Inlines.Add(new Hyperlink(new Run("second caption")) { NavigateUri = target });
        var otherLink = new Hyperlink(new Run("other link")) { NavigateUri = other, FontSize = 23 };
        var external = new Hyperlink(new Run("external")) { NavigateUri = new Uri("https://example.com/" + target) };
        paragraphs[2].Inlines.Add(otherLink);
        paragraphs[2].Inlines.Add(external);
        var listLink = new Hyperlink(new Run("list caption")) {
            NavigateUri = new Uri(target.OriginalString.Replace("#B", "#%42"), UriKind.Relative)
        };
        editor.Document.Blocks.Add(new System.Windows.Documents.List(new ListItem(new Paragraph(listLink))));
        var tableLink = new Hyperlink(new Run("table caption")) { NavigateUri = target };
        var table = new Table();
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(tableLink)));
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        editor.Document.Blocks.Add(table);
        paragraphs[2].Inlines.Add(new Floater(new Paragraph(
            new Hyperlink(new Run("floating caption")) { NavigateUri = target })));
        string before = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.Replace("\u200B", "");
        var window = new Window { Content = editor.Service, ShowInTaskbar = false, ShowActivated = false };
        window.Show();
        try
        {
            editor.Service.IsUndoEnabled = false;
            editor.Service.IsUndoEnabled = true;
            using var model = new BookmarksModel(editor, bookmarks, new BookmarkValidationService(bookmarks));
            using var menu = new BookmarksViewModel(model, new StubWindowManager());
            menu.SelectedBookmark = bookmarks.Bookmarks[0];
            menu.Remove.Execute(null);

            Assert.Equal(new[] { otherLink, external }, new FlowDocumentWalker().Find<Hyperlink>(editor.Document));
            string expected = before;
            foreach(string deleted in new[] { "Target", "first caption", "second caption", "list caption", "table caption", "floating caption" })
                expected = expected.Replace(deleted, "");
            Assert.Equal(expected, new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            Assert.Equal(23, otherLink.FontSize);
            Assert.True(bookmarks.Exists("Other"));
            Assert.False(bookmarks.Exists("Target"));
            Assert.Null(menu.SelectedBookmark);
            Assert.True(editor.CanUndo);
            editor.Undo();
            Assert.False(editor.CanUndo);
            bookmarks.RebuildIndexFromDocument(editor);
            Assert.True(bookmarks.Exists("Target"));
            Assert.Equal(before, new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            Assert.Equal(7, new FlowDocumentWalker().Find<Hyperlink>(editor.Document).Count());
            Assert.True(editor.CanRedo);
            editor.Redo();
            bookmarks.RebuildIndexFromDocument(editor);
            Assert.Equal(expected, new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            Assert.False(bookmarks.Exists("Target"));
            Assert.True(bookmarks.Exists("Other"));
            Assert.Equal(2, new FlowDocumentWalker().Find<Hyperlink>(editor.Document).Count());
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public void RemoveLegacyAnchor_DeletesItsContentAndNestedBookmarksAndTheirReferences()
    {
        var editor = CreateRichText("suffix");
        var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock!;
        var inner = new Span(new Run("inner text")) { Name = "Inner" };
        var outer = new Span(new Bold(new Run("visible text"))) { Name = "Outer", Foreground = Brushes.DarkGreen };
        outer.Inlines.Add(inner);
        paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline!, outer);
        paragraph.Inlines.Add(new Hyperlink(new Run("caption")) { NavigateUri = new Uri("#Outer", UriKind.Relative) });
        paragraph.Inlines.Add(new Hyperlink(new Run("inner caption")) { NavigateUri = new Uri("#Inner", UriKind.Relative) });
        paragraph.Inlines.Add(new Span(new Run("other text")) { Name = "Other" });
        var bookmarks = new BookmarksService(editor);
        bookmarks.RebuildIndexFromDocument(editor);

        Assert.True(bookmarks.Remove(editor, "Outer"));
        Assert.Equal("suffixother text", new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
        Assert.Empty(new FlowDocumentWalker().Find<Hyperlink>(editor.Document));
        Assert.False(bookmarks.Exists("Outer"));
        Assert.False(bookmarks.Exists("Inner"));
        Assert.Equal("Other", Assert.Single(bookmarks.Bookmarks).Name);
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.Equal("Other", Assert.Single(bookmarks.Bookmarks).Name);
    }

    [WpfFact]
    public void Remove_DeletesBookmarksInsideLinkCaptionsAndTheirReferences()
    {
        var editor = CreateRichText("suffix", "links");
        var paragraph = (Paragraph)editor.Document.Blocks.LastBlock!;
        var bookmarks = new BookmarksService(editor);
        bookmarks.AddAtCaret(editor, "Target");
        var target = bookmarks.Bookmarks[0].BookmarkUri!;
        paragraph.Inlines.Add(new Hyperlink(new Span(new Run("nested text")) { Name = "Nested" }) {
            NavigateUri = target
        });
        paragraph.Inlines.Add(new Hyperlink(new Span(new Run("deeper text")) { Name = "Deeper" }) {
            NavigateUri = new Uri("#Nested", UriKind.Relative)
        });
        paragraph.Inlines.Add(new Hyperlink(new Run("last caption")) {
            NavigateUri = new Uri("#Deeper", UriKind.Relative)
        });
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.Equal(3, bookmarks.Bookmarks.Count);

        Assert.True(bookmarks.Remove(editor, "Target"));

        Assert.Empty(bookmarks.Bookmarks);
        Assert.Empty(new FlowDocumentWalker().Find<Hyperlink>(editor.Document));
        Assert.Equal("suffix\r\nlinks\r\n", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.Empty(bookmarks.Bookmarks);
    }

    [WpfTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HyperlinkFromSelection_IsRejectedInPanelManagerAndService(bool multipleParagraphs, bool manager)
    {
        var editor = CreateRichText("target", "selected text", "second paragraph");
        var bookmarks = new BookmarksService(editor);
        bookmarks.AddAtCaret(editor, "Target");
        var validation = new BookmarkValidationService(bookmarks);
        using var model = new BookmarksModel(editor, bookmarks, validation);
        using var menu = new BookmarksViewModel(model, new StubWindowManager());
        using var editorModel = new BookmarksEditorModel(model, new StubWindowManager());
        using var managerViewModel = new BookmarksEditorViewModel(editorModel);
        menu.SelectedBookmark = Assert.Single(bookmarks.Bookmarks);
        menu.LinkText = "Own caption";
        var command = manager ? managerViewModel.InsertHyperlinkTo : menu.InsertHyperlinkTo;
        var paragraphs = editor.Document.Blocks.Cast<Paragraph>().ToArray();
        editor.Selection.Select(paragraphs[1].ContentStart,
            paragraphs[multipleParagraphs ? 2 : 1].ContentEnd);
        string before = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
        string selected = editor.Selection.Text;

        var validationResult = validation.CanInsertHyperlink(editor, menu.LinkText);
        Assert.Equal(ValidationCode.BadSelection, validationResult.Code);
        Assert.False(command.CanExecute(null));
        command.Execute(null);
        Assert.Throws<InvalidOperationException>(() => bookmarks.InsertHyperlinkTo(editor, "Target", "Own caption"));
        Assert.Empty(new FlowDocumentWalker().Find<Hyperlink>(editor.Document));
        Assert.Equal(before, new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
        Assert.Equal(selected, editor.Selection.Text);
        Assert.Equal("Own caption", menu.LinkText);

        editor.ClearSelection();
        menu.LinkText = string.Empty;
        Assert.Equal(ValidationCode.NeedLinkText, validation.CanInsertHyperlink(editor, menu.LinkText).Code);
        Assert.False(command.CanExecute(null));
        menu.LinkText = "Own caption";
        Assert.True(command.CanExecute(null));
        command.Execute(null);
        var link = Assert.Single(new FlowDocumentWalker().Find<Hyperlink>(editor.Document));
        Assert.Equal("Own caption", new TextRange(link.ContentStart, link.ContentEnd).Text);
        Assert.Contains("selected text", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
    }

    [WpfTheory]
    [InlineData("ab", 0)]
    [InlineData("ab", 1)]
    [InlineData("ab", 2)]
    [InlineData("", 0)]
    public void HyperlinkInsertion_KeepsOwnCaptionSeparateFromSurroundingAndFollowingText(string text, int offset)
    {
        var editor = CreateRichText("target", text);
        var bookmarks = new BookmarksService(editor);
        bookmarks.AddAtCaret(editor, "Target");
        var paragraph = (Paragraph)editor.Document.Blocks.LastBlock!;
        var run = (Run)paragraph.Inlines.FirstInline!;
        editor.CaretPosition = run.ContentStart.GetPositionAtOffset(offset)!;

        Assert.True(new BookmarkValidationService(bookmarks).CanInsertHyperlink(editor, "Own caption").Ok);
        bookmarks.InsertHyperlinkTo(editor, "Target", "Own caption");
        var link = Assert.Single(new FlowDocumentWalker().Find<Hyperlink>(editor.Document));
        Assert.Equal("Own caption", new TextRange(link.ContentStart, link.ContentEnd).Text);
        Assert.Equal(text.Insert(offset, "Own caption"), new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
        Assert.True(editor.Selection.IsEmpty);
        editor.InsertTextAtCaret("!");
        Assert.Equal("Own caption", new TextRange(link.ContentStart, link.ContentEnd).Text);
        Assert.True(bookmarks.Remove(editor, "Target"));
        Assert.Equal(text.Insert(offset, "!"), new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
        Assert.Empty(new FlowDocumentWalker().Find<Hyperlink>(editor.Document));
    }

    [WpfFact]
    public void BookmarksEditorIcon_IsAvailableAsWpfResource()
    {
        var uri = new Uri(
            "/CryptoBook;component/Resources/Icons/AppIcon.ico",
            UriKind.Relative);

        var resource = Application.GetResourceStream(uri);

        Assert.NotNull(resource);
        Assert.True(resource.Stream.Length > 0);
    }

    [WpfFact]
    public void AddRenameNavigateAndRemove_KeepDocumentAndIndexSynchronized()
    {
        var richText = CreateRichText("text");
        var service = new BookmarksService(richText);
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;

        richText.CaretPosition = paragraph.ContentStart;
        service.AddAtCaret(richText, "Начало раздела");
        var first = Assert.Single(service.Bookmarks);
        var anchorId = first.BookmarkUri!.OriginalString.TrimStart('#');

        richText.CaretPosition = paragraph.ContentEnd;
        service.AddAtCaret(richText, "Конец");

        Assert.Equal(2, service.Bookmarks.Count);
        Assert.True(service.Exists("начало раздела"));
        Assert.NotNull(FindAnchor(richText.Document, anchorId));

        service.Rename(richText, "Начало раздела", "Введение");
        Assert.False(service.Exists("Начало раздела"));
        Assert.True(service.Exists("Введение"));
        Assert.Equal($"#{anchorId}", first.BookmarkUri!.OriginalString);

        Assert.True(service.NavigateTo(richText, "Введение"));
        AssertCaretInside(
            FindAnchor(richText.Document, anchorId)!,
            richText.CaretPosition);

        Assert.True(service.Remove(richText, "Введение"));
        Assert.False(service.Exists("Введение"));
        Assert.Null(FindAnchor(richText.Document, anchorId));
        Assert.Single(service.Bookmarks);
    }

    [WpfFact]
    public void NextAndPrevious_NavigateInDocumentOrderAndWrap()
    {
        var richText = CreateRichText("first", "second");
        var service = new BookmarksService(richText);
        var firstParagraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var secondParagraph = (Paragraph)richText.Document.Blocks.LastBlock!;

        richText.CaretPosition = firstParagraph.ContentStart;
        service.AddAtCaret(richText, "First");
        richText.CaretPosition = secondParagraph.ContentStart;
        service.AddAtCaret(richText, "Second");

        Assert.True(service.NavigateTo(richText, "First"));
        Assert.True(service.NavigateNext(richText));
        AssertCaretAt(service.Bookmarks[1], richText);
        Assert.True(service.NavigateNext(richText));
        AssertCaretAt(service.Bookmarks[0], richText);
        Assert.True(service.NavigatePrevious(richText));
        AssertCaretAt(service.Bookmarks[1], richText);
    }

    [WpfFact]
    public void HyperlinkInsertion_SplitsAdjacentTextAndSurvivesBookmarkRename()
    {
        var richText = CreateRichText("target", "ab");
        var service = new BookmarksService(richText);
        var targetParagraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var textParagraph = (Paragraph)richText.Document.Blocks.LastBlock!;

        richText.CaretPosition = targetParagraph.ContentStart;
        service.AddAtCaret(richText, "Target");
        var anchorUri = service.Bookmarks[0].BookmarkUri;

        var run = textParagraph.Inlines.OfType<Run>()
            .Single(item => item.Text == "ab");
        richText.CaretPosition = run.ContentStart.GetPositionAtOffset(1)!;
        richText.ClearSelection();
        service.InsertHyperlinkTo(richText, "Target", "X");

        Assert.Equal("aXb", new TextRange(
            textParagraph.ContentStart,
            textParagraph.ContentEnd).Text);
        var hyperlink = Assert.Single(textParagraph.Inlines.OfType<Hyperlink>());
        Assert.Equal(anchorUri, hyperlink.NavigateUri);

        service.Rename(richText, "Target", "Renamed target");
        Assert.Equal(anchorUri, hyperlink.NavigateUri);
        Assert.True(service.NavigateTo(richText, "Renamed target"));
    }

    [WpfFact]
    public void RebuildIndex_RestoresNamesAndNotesFromDocumentMetadata()
    {
        var richText = CreateRichText("text");
        var original = new BookmarksService(richText);
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        richText.CaretPosition = paragraph.ContentStart;
        original.AddAtCaret(richText, "Раздел 1");
        original.Bookmarks[0].Note = "Важная заметка";

        var rebuilt = new BookmarksService(richText);
        rebuilt.RebuildIndexFromDocument(richText);

        var bookmark = Assert.Single(rebuilt.Bookmarks);
        Assert.Equal("Раздел 1", bookmark.Name);
        Assert.Equal("Важная заметка", bookmark.Note);
        Assert.True(rebuilt.NavigateTo(richText, bookmark.Name));
    }

    [WpfFact]
    public void ViewModelCommands_PerformRealBookmarkOperations()
    {
        var richText = CreateRichText("text");
        var bookmarkService = new BookmarksService(richText);
        var validation = new BookmarkValidationService(bookmarkService);
        var model = new BookmarksModel(
            richText,
            bookmarkService,
            validation);
        var viewModel = new BookmarksViewModel(
            model,
            new StubWindowManager());

        viewModel.NewBookmarkName = "My bookmark";
        Assert.True(viewModel.AddAtCaret.CanExecute(null));
        viewModel.AddAtCaret.Execute(null);

        viewModel.SelectedBookmark = Assert.Single(viewModel.Bookmarks);
        viewModel.RenameTo = "Renamed";
        Assert.True(viewModel.Rename.CanExecute(null));
        viewModel.Rename.Execute(null);
        Assert.True(bookmarkService.Exists("Renamed"));

        Assert.True(viewModel.NavigateTo.CanExecute(null));
        viewModel.NavigateTo.Execute(null);
        Assert.Equal(
            LocalizationManager.Format(
                "Bookmarks.Renamed",
                "My bookmark",
                "Renamed"),
            viewModel.StatusMessage);

        Assert.True(viewModel.Remove.CanExecute(null));
        viewModel.Remove.Execute(null);
        Assert.Empty(viewModel.Bookmarks);
    }

    [WpfFact]
    public void EditorViewModel_DelegatesBookmarkOperationsToEditorModel()
    {
        var richText = CreateRichText("text");
        var bookmarkService = new BookmarksService(richText);
        var validation = new BookmarkValidationService(bookmarkService);
        var bookmarks = new BookmarksModel(
            richText,
            bookmarkService,
            validation);
        var editorModel = new BookmarksEditorModel(
            bookmarks,
            new StubWindowManager());
        var viewModel = new BookmarksEditorViewModel(editorModel);

        bookmarks.NewBookmarkName = "Original";
        bookmarks.AddAtCaret();
        viewModel.SelectedBookmark = Assert.Single(viewModel.Bookmarks);
        viewModel.RenameTo = "Renamed in editor";

        Assert.True(viewModel.Rename.CanExecute(null));
        viewModel.Rename.Execute(null);

        Assert.True(bookmarkService.Exists("Renamed in editor"));
        Assert.Equal("Renamed in editor", viewModel.SelectedBookmark!.Name);
        Assert.True(viewModel.Remove.CanExecute(null));
        viewModel.Remove.Execute(null);
        Assert.Empty(bookmarkService.Bookmarks);
        Assert.Equal("text\r\n", new TextRange(richText.Document.ContentStart, richText.Document.ContentEnd).Text);
    }

    private static void AssertCaretAt(
        IBookmarkEntryViewModel bookmark,
        IRichTextBoxService richText)
    {
        var anchorId = bookmark.BookmarkUri!.OriginalString.TrimStart('#');
        var anchor = Assert.IsType<Span>(FindAnchor(richText.Document, anchorId));
        AssertCaretInside(anchor, richText.CaretPosition);
    }

    private static void AssertCaretInside(Span anchor, TextPointer caret)
    {
        Assert.True(anchor.ContentStart.CompareTo(caret) <= 0);
        Assert.True(anchor.ContentEnd.CompareTo(caret) >= 0);
    }

    private static Span? FindAnchor(FlowDocument document, string anchorId)
    {
        for(var position = document.ContentStart;
            position != null && position.CompareTo(document.ContentEnd) < 0;
            position = position.GetNextContextPosition(LogicalDirection.Forward))
        {
            if(position.GetAdjacentElement(LogicalDirection.Forward) is Span span &&
               string.Equals(span.Name, anchorId, StringComparison.Ordinal))
            {
                return span;
            }
        }

        return null;
    }

    private static IRichTextBoxService CreateRichText(params string[] texts)
    {
        IRichTextBoxService richText =
            new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService(),
                new DocumentAppearanceDefaults());
        richText.Document.Blocks.Clear();
        foreach(var text in texts)
            richText.Document.Blocks.Add(new Paragraph(new Run(text)));
        richText.CaretPosition =
            ((Paragraph)richText.Document.Blocks.FirstBlock!).ContentStart;
        richText.ClearSelection();
        return richText;
    }

    private sealed class TestParagraphFactory: IParagraphFactory
    {
        public IParagraphService Create(Inline? inline = null)
        {
            var paragraph = new ParagraphService();
            if(inline != null)
                paragraph.Inlines.Add(inline);
            return paragraph;
        }
    }

    private sealed class StubWindowManager: IWindowManager
    {
        public Guid CreateWindow<T>(
            IReadOnlyDictionary<string, object?>? args = null)
            where T: System.Windows.Window => Guid.NewGuid();
        public TResult? GetResult<TResult>(Guid guid) => default;
        public void ShowWindow(Guid windowId) { }
        public void ShowWindowDialog(Guid windowId) { }
        public void ActivateWindow(Guid windowId) { }
        public void CloseWindow(Guid windowId) { }
        public bool IsWindowOpen(Guid windowId) => false;
        public CryptoBook.DTO.WindowHost? FindHostWindow(Guid windowId) => null;
    }
}

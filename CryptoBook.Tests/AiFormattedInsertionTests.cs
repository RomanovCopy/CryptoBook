using CryptoBook.AI;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

using Xunit;

namespace CryptoBook.Tests;

public sealed class AiFormattedInsertionTests
{
    private const string Reply = "# Heading\n\n**bold** and *italic*\n\n- item\n\n| Name | Cost |\n|---|---|\n| Book | 10 |\n\n```cs\nint n = 1;\n```";

    [WpfTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void InsertAndReplace_InParagraphOrCell_PreservesSurroundingTextAndUndo(bool inCell, bool replace)
    {
        var editor = TableEditingTests.CreateEditor(2, 2);
        using var host = new TableEditingTests.EditorHost(editor);
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(editor, markdown);
        session.Open("sample.XamlPackage", new XamlPackageFileTemplate(), editor.Document);
        var paragraph = inCell
            ? (Paragraph)editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[0].Blocks.FirstBlock!
            : (Paragraph)editor.Document.Blocks.FirstBlock!;
        paragraph.Inlines.Clear();
        var run = new Run("left selected right");
        paragraph.Inlines.Add(run);
        var start = run.ContentStart.GetPositionAtOffset(5)!;
        editor.Selection.Select(start, start.GetPositionAtOffset(8)!);
        string before = Text(editor);
        using var context = new AiEditorContext(editor, markdown, session, new HomeNavigation());
        var target = context.Capture()!;
        editor.Service.IsUndoEnabled = false;
        editor.Service.IsUndoEnabled = true;

        Assert.True(target.Apply(Reply, replace));
        string after = Text(editor);
        Assert.Contains("left ", after);
        Assert.Contains(" right", after);
        Assert.Equal(!replace, after.Contains("selected", StringComparison.Ordinal));
        Assert.Contains("Heading", after);
        Assert.Contains("Book", after);
        Assert.DoesNotContain("**bold**", after);
        Assert.Contains("R0C1", after);
        Assert.Contains("after", after);
        Assert.NotNull(editor.CaretPosition.Paragraph);
        editor.Undo();
        Assert.Equal(before, Text(editor));
        editor.Redo();
        Assert.Equal(after, Text(editor));
    }

    [WpfFact]
    public void InsertedTable_IsIndependentEditableAndRejectsStaleTarget()
    {
        var editor = TableEditingTests.CreateEditor(1, 1);
        var session = new DocumentSession(editor, new MarkdownDocumentState());
        session.Open("sample.XamlPackage", new XamlPackageFileTemplate(), editor.Document);
        editor.Selection.Select(editor.Document.ContentStart, editor.Document.ContentEnd);
        using var context = new AiEditorContext(editor, new MarkdownDocumentState(), session, new HomeNavigation());
        var target = context.Capture()!;
        Assert.True(target.Apply(Reply, true));
        var table = Assert.Single(editor.Document.Blocks.OfType<Table>());
        var cell = table.RowGroups[0].Rows[1].Cells[1];
        editor.Selection.Select(cell.ContentStart, cell.ContentEnd);
        editor.Selection.Text = "20";
        Assert.Contains("20", Text(editor));
        Assert.True(new TableEditingService(editor).Edit(TableEditOperation.InsertColumnRight));
        Assert.Equal(3, table.RowGroups[0].Rows[0].Cells.Count);
        Assert.False(target.Apply("stale", true));
    }

    [WpfFact]
    public void InlineFormatting_IsInsertedWithoutExtraParagraph_AndLinksRemainInertForLocalFiles()
    {
        var editor = TableEditingTests.CreateEditor(1, 1);
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(editor, markdown);
        session.Open("sample.XamlPackage", new XamlPackageFileTemplate(), editor.Document);
        var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock!;
        editor.Selection.Select(paragraph.ContentEnd, paragraph.ContentEnd);
        using var context = new AiEditorContext(editor, markdown, session, new HomeNavigation());
        Assert.True(context.Capture()!.Apply("**bold** [local](file:///C:/private.txt) ![remote](https://example.com/private.png)", false));
        Assert.Equal(3, editor.Document.Blocks.Count);
        Assert.Contains("beforebold", Text(editor));
        var walker = new CryptoBook.Infrastructure.FlowDocumentWalker();
        Assert.Contains(walker.Traverse(editor.Document), element => element is Run run && run.Text == "bold" && run.FontWeight == FontWeights.Bold);
        Assert.DoesNotContain(walker.Traverse(editor.Document), element => element is Hyperlink or InlineUIContainer or BlockUIContainer);
    }

    private static string Text(IRichTextBoxService editor) => new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;

    internal sealed class HomeNavigation : IPageNavigationService
    {
        public string CurrentKey => "Home";
        public Page? CurrentPage => null;
        public bool CanGoBack => false;
        public bool CanGoForward => false;
        public IReadOnlyList<string>? Keys => null;
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public void Navigate(string key, object? args = null) => throw new NotSupportedException();
        public void GoBack() => throw new NotSupportedException();
        public void GoForward() => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
    }
}

using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Security;

using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class TableEditingTests
{
    [WpfTheory]
    [InlineData(TableEditOperation.InsertRowAbove, 0)]
    [InlineData(TableEditOperation.InsertRowAbove, 1)]
    [InlineData(TableEditOperation.InsertRowBelow, 2)]
    [InlineData(TableEditOperation.InsertColumnLeft, 0)]
    [InlineData(TableEditOperation.InsertColumnLeft, 1)]
    [InlineData(TableEditOperation.InsertColumnRight, 2)]
    [InlineData(TableEditOperation.DeleteRow, 0)]
    [InlineData(TableEditOperation.DeleteRow, 1)]
    [InlineData(TableEditOperation.DeleteRow, 2)]
    [InlineData(TableEditOperation.DeleteColumn, 0)]
    [InlineData(TableEditOperation.DeleteColumn, 1)]
    [InlineData(TableEditOperation.DeleteColumn, 2)]
    public void StructuralEdit_PreservesOtherCells_AndUndoRedoRestoresWidths(TableEditOperation operation, int index)
    {
        var editor = CreateEditor(3, 3);
        using var host = new EditorHost(editor);
        var table = GetTable(editor);
        var cell = table.RowGroups[0].Rows[index].Cells[index];
        editor.Selection.Select(cell.ContentStart, cell.ContentStart);
        var service = new TableEditingService(editor);
        string before = Snapshot(editor);
        ResetUndo(editor);

        Assert.True(service.CanEdit);
        Assert.True(service.Edit(operation));
        string after = Snapshot(editor);
        Assert.NotEqual(before, after);
        bool rowOperation = operation is TableEditOperation.InsertRowAbove or TableEditOperation.InsertRowBelow or TableEditOperation.DeleteRow;
        bool delete = operation is TableEditOperation.DeleteRow or TableEditOperation.DeleteColumn;
        table = GetTable(editor);
        Assert.Equal(rowOperation ? (delete ? 2 : 4) : 3, table.RowGroups[0].Rows.Count);
        Assert.All(table.RowGroups[0].Rows, row => Assert.Equal(rowOperation ? 3 : (delete ? 2 : 4), row.Cells.Count));
        for(int r = 0; r < 3; r++)
        for(int c = 0; c < 3; c++)
        {
            bool removed = operation == TableEditOperation.DeleteRow && r == index ||
                operation == TableEditOperation.DeleteColumn && c == index;
            Assert.Equal(!removed, Text(editor.Document).Contains($"R{r}C{c}", StringComparison.Ordinal));
        }
        Assert.Contains("before", Text(editor.Document));
        Assert.Contains("after", Text(editor.Document));
        Assert.NotNull(editor.CaretPosition.Paragraph);

        editor.Undo();
        Assert.Equal(before, Snapshot(editor));
        editor.Redo();
        Assert.Equal(after, Snapshot(editor));
    }

    [WpfTheory]
    [InlineData(TableEditOperation.DeleteRow)]
    [InlineData(TableEditOperation.DeleteColumn)]
    public void LastRowOrColumn_RemovesTable_AndUndoRestoresContent(TableEditOperation operation)
    {
        var editor = CreateEditor(1, 1);
        using var host = new EditorHost(editor);
        var table = GetTable(editor);
        var cell = table.RowGroups[0].Rows[0].Cells[0];
        editor.CaretPosition = cell.ContentStart;
        string before = Snapshot(editor);
        ResetUndo(editor);

        Assert.True(new TableEditingService(editor).Edit(operation));
        Assert.Empty(editor.Document.Blocks.OfType<Table>());
        Assert.NotNull(editor.CaretPosition.Paragraph);
        Assert.Contains("before", Text(editor.Document));
        Assert.Contains("after", Text(editor.Document));
        editor.Undo();
        Assert.Equal(before, Snapshot(editor));
    }

    [WpfFact]
    public void ReadOnlySpansRaggedRowsAndCrossCellSelection_AreRejectedWithoutChanges()
    {
        var editor = CreateEditor(2, 2);
        var table = GetTable(editor);
        var rows = table.RowGroups[0].Rows;
        var first = rows[0].Cells[0];
        editor.CaretPosition = first.ContentStart;
        var service = new TableEditingService(editor);
        editor.IsReadOnly = true;
        Assert.False(service.Edit(TableEditOperation.DeleteRow));
        editor.IsReadOnly = false;
        first.ColumnSpan = 2;
        string before = Snapshot(editor);
        Assert.False(service.CanEdit);
        Assert.False(service.Edit(TableEditOperation.InsertRowBelow));
        Assert.Equal(before, Snapshot(editor));
        first.ColumnSpan = 1;
        first.RowSpan = 2;
        Assert.False(service.CanEdit);
        first.RowSpan = 1;
        rows[1].Cells.RemoveAt(1);
        Assert.False(service.CanEdit);
        rows[1].Cells.Add(new TableCell(new Paragraph()));
        editor.Selection.Select(first.ContentStart, rows[1].Cells[1].ContentEnd);
        Assert.False(service.CanEdit);
        editor.CaretPosition = editor.Document.Blocks.FirstBlock!.ContentStart;
        Assert.False(service.CanEdit);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MarkdownTable_EditSaveReopen_ContinuesEditingWithDirtyRevision(bool encrypted)
    {
        var document = new MarkdownFlowDocumentRenderer().Render("| Name | Cost |\n|---|---|\n| Book | 10 |", null);
        var editor = CreateEditor(1, 1);
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(editor, markdown);
        session.Open("table.xamlpackage", new XamlPackageFileTemplate(), document);
        var table = GetTable(editor);
        table.Columns.Add(new TableColumn { Width = new GridLength(140) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });
        editor.CaretPosition = table.RowGroups[0].Rows[1].Cells[1].ContentStart;
        var service = new TableEditingService(editor);
        long revision = session.Revision;
        Assert.True(service.Edit(TableEditOperation.InsertColumnRight));
        editor.Selection.Text = "Comment";
        Assert.True(session.Revision > revision);
        var handler = new XamlPackageDocumentFormatHandler(new WpfDispatcherService(Dispatcher.CurrentDispatcher));
        var loaded = new FlowDocument();
        var save = new FlowDocumentSaveService(new WpfDispatcherService(Dispatcher.CurrentDispatcher),
            new DocumentFormatHandlerRegistry([handler]));
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, encrypted ? "table.cbook" : "table.XamlPackage");
        try
        {
            if(encrypted)
            {
                using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
                key.SetKey("vQ8!Mz3#Rk6@Yp2&");
                var codec = new SecureFileV2Codec(key, new SecureFileV2Options
                {
                    KeyDerivation = new KeyDerivationParameters(1, 8 * 1024, 1, 32), ChunkSize = 4096
                });
                var processor = new SecureFileProcessor(codec, new LegacySecureFileCodec(key));
                using var payload = new MemoryStream();
                await save.SaveToStreamAsync(editor, payload, new SecureFileTemplate());
                payload.Position = 0;
                await processor.EncryptStreamAsync(payload, ".XamlPackage", path);
                await using var decrypted = await processor.DecryptFileContentAsync(path);
                Assert.Equal(".XamlPackage", decrypted.OriginalExtension);
                using var restored = new MemoryStream();
                await decrypted.Content.CopyToAsync(restored);
                await handler.LoadAsync(loaded, restored.ToArray());
            }
            else
            {
                await save.SaveToFileAsync(editor, path, new XamlPackageFileTemplate());
                await handler.LoadAsync(loaded, await File.ReadAllBytesAsync(path));
            }
            Assert.Single(Directory.EnumerateFiles(directory));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
        editor.ReplaceDocument(loaded);
        table = GetTable(editor);
        Assert.Equal(3, table.RowGroups[0].Rows[0].Cells.Count);
        Assert.Equal(new[] { 140d, 90d, 90d }, table.Columns.Select(column => column.Width.Value));
        Assert.Contains("Comment", Text(loaded));
        editor.CaretPosition = table.RowGroups[0].Rows[1].Cells[2].ContentStart;
        Assert.True(service.Edit(TableEditOperation.InsertRowBelow));
        Assert.Equal(3, GetTable(editor).RowGroups[0].Rows.Count);
    }

    [WpfFact]
    public void ColumnEdits_PreserveLiveContentResourcesAndBookmarks_AcrossUndoAndRedo()
    {
        var editor = CreateEditor(2, 2);
        using var host = new EditorHost(editor);
        var table = GetTable(editor);
        table.Tag = "table metadata";
        table.Resources["cellForeground"] = Brushes.Blue;
        var cell = table.RowGroups[0].Rows[1].Cells[1];
        cell.SetResourceReference(TextElement.ForegroundProperty, "cellForeground");
        var image = new System.Windows.Controls.Image { Width = 40, Height = 20 };
        ((Paragraph)cell.Blocks.FirstBlock!).Inlines.Add(new InlineUIContainer(image));
        var bookmarks = new BookmarksService(editor);
        editor.CaretPosition = cell.ContentStart;
        bookmarks.AddAtCaret(editor, "Keep");
        bookmarks.Bookmarks[0].Note = "Note";
        editor.CaretPosition = table.RowGroups[0].Rows[1].Cells[0].ContentStart;
        using var service = new TableEditingService(editor, bookmarks);
        ResetUndo(editor);
        string before = Snapshot(editor);

        Assert.True(service.Edit(TableEditOperation.InsertColumnLeft));
        table = GetTable(editor);
        Assert.Same(cell, table.RowGroups[0].Rows[1].Cells[2]);
        Assert.Same(image, Assert.IsType<InlineUIContainer>(((Paragraph)cell.Blocks.FirstBlock!).Inlines.LastInline).Child);
        Assert.Equal("table metadata", table.Tag);
        Assert.Equal(Brushes.Blue, cell.Foreground);
        Assert.True(bookmarks.NavigateTo(editor, "Keep"));
        Assert.Equal("Note", Assert.Single(bookmarks.Bookmarks).Note);
        Assert.True(service.Edit(TableEditOperation.DeleteColumn));
        Assert.Empty(bookmarks.Bookmarks);
        editor.Undo();
        Assert.True(bookmarks.NavigateTo(editor, "Keep"));
        editor.Undo();
        Assert.Equal(before, Snapshot(editor));
        editor.Redo();
        editor.Redo();
        Assert.Empty(bookmarks.Bookmarks);
    }

    [WpfFact]
    public void RowGroupsAndNestedTables_KeepCorrectTargetAndEditableOwner()
    {
        var editor = CreateEditor(2, 2);
        var outer = GetTable(editor);
        var group = new TableRowGroup();
        outer.RowGroups.Add(group);
        var row = outer.RowGroups[0].Rows[1];
        outer.RowGroups[0].Rows.Remove(row);
        group.Rows.Add(row);
        var nested = new Table();
        var nestedGroup = new TableRowGroup();
        var nestedRow = new TableRow();
        nestedRow.Cells.Add(new TableCell(new Paragraph(new Run("nested"))));
        nestedGroup.Rows.Add(nestedRow);
        nested.RowGroups.Add(nestedGroup);
        row.Cells[1].Blocks.Clear();
        row.Cells[1].Blocks.Add(nested);
        editor.CaretPosition = nestedRow.Cells[0].ContentStart;
        using var service = new TableEditingService(editor);
        Assert.True(service.Edit(TableEditOperation.DeleteRow));
        Assert.Single(row.Cells[1].Blocks.OfType<Paragraph>());
        Assert.Equal(2, outer.RowGroups.Count);
        editor.CaretPosition = row.Cells[0].ContentStart;
        Assert.True(service.Edit(TableEditOperation.DeleteRow));
        Assert.Single(outer.RowGroups);
        Assert.NotNull(editor.CaretPosition.Paragraph);
    }

    internal static IRichTextBoxService CreateEditor(int rows, int columns)
    {
        IRichTextBoxService editor = new RichTextBoxService(new ParagraphFactory(), new UriNavigation(), new DocumentAppearanceDefaults());
        var table = new Table { CellSpacing = 0 };
        for(int c = 0; c < columns; c++)
            table.Columns.Add(new TableColumn { Width = new GridLength(80 + c * 25) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        for(int r = 0; r < rows; r++)
        {
            var row = new TableRow();
            group.Rows.Add(row);
            for(int c = 0; c < columns; c++)
                row.Cells.Add(new TableCell(new Paragraph(new Run($"R{r}C{c}")))
                {
                    Padding = new Thickness(5), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray,
                    FontWeight = r == 0 ? FontWeights.Bold : FontWeights.Normal
                });
        }
        editor.ReplaceDocument(new FlowDocument());
        editor.Document.Blocks.Add(new Paragraph(new Run("before")));
        editor.Document.Blocks.Add(table);
        editor.Document.Blocks.Add(new Paragraph(new Run("after")));
        return editor;
    }

    private static Table GetTable(IRichTextBoxService editor) => Assert.Single(editor.Document.Blocks.OfType<Table>());
    private static string Text(FlowDocument document) => new TextRange(document.ContentStart, document.ContentEnd).Text;
    private static string Snapshot(IRichTextBoxService editor)
    {
        var table = GetTable(editor);
        return Text(editor.Document) + "|" + string.Join(",", table.Columns.Select(column => column.Width.ToString())) + "|" +
            string.Join(";", table.RowGroups.SelectMany(group => group.Rows).Select(row => row.Cells.Count));
    }
    private static void ResetUndo(IRichTextBoxService editor)
    {
        editor.Service.IsUndoEnabled = false;
        editor.Service.IsUndoEnabled = true;
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
    private sealed class UriNavigation : IUriNavigationService
    {
        public bool TryOpen(Uri uri) => false;
    }

    internal sealed class EditorHost : IDisposable
    {
        private readonly Window window;
        public EditorHost(IRichTextBoxService editor)
        {
            window = new Window { Content = editor.Service, Width = 600, Height = 400, ShowInTaskbar = false, ShowActivated = false };
            window.Show();
            editor.Service.UpdateLayout();
        }
        public void Dispose() => window.Close();
    }
}

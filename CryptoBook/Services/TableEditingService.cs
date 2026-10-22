using CryptoBook.Interfaces;
using CryptoBook.Infrastructure;

using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Services;

/// <summary>Structural edits for rectangular tables in the active rich-text editor.</summary>
public sealed class TableEditingService : ITableEditingService, IDisposable
{
    private readonly IRichTextBoxService editor;
    private readonly IBookmarkService? bookmarks;

    public TableEditingService(IRichTextBoxService editor, IBookmarkService? bookmarks = null)
    {
        this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
        this.bookmarks = bookmarks;
        if(bookmarks is not null)
            editor.Service.TextChanged += OnTextChanged;
    }

    public bool CanEdit => TryGetCell() is not null;

    public bool Edit(TableEditOperation operation)
    {
        var cell = TryGetCell();
        if(cell is null || !Enum.IsDefined(operation))
            return false;

        var row = (TableRow)cell.Parent;
        var group = (TableRowGroup)row.Parent;
        var table = (Table)group.Parent;
        int columnIndex = row.Cells.IndexOf(cell);
        int rowIndex = group.Rows.IndexOf(row);
        var rows = table.RowGroups.SelectMany(item => item.Rows).ToArray();

        editor.BeginChange();
        try
        {
            if(table.Columns.Count > 0 && (operation is TableEditOperation.InsertColumnLeft or
                TableEditOperation.InsertColumnRight || operation == TableEditOperation.DeleteColumn && row.Cells.Count > 1))
                table = ReplaceTableShell(table);

            TableCell? destination = cell;
            switch(operation)
            {
                case TableEditOperation.InsertRowAbove:
                case TableEditOperation.InsertRowBelow:
                    var newRow = new TableRow();
                    CopyFormatting(row, newRow);
                    foreach(var source in row.Cells)
                        newRow.Cells.Add(CreateCell(source));
                    group.Rows.Insert(rowIndex + (operation == TableEditOperation.InsertRowBelow ? 1 : 0), newRow);
                    destination = newRow.Cells[columnIndex];
                    break;

                case TableEditOperation.InsertColumnLeft:
                case TableEditOperation.InsertColumnRight:
                    int insertIndex = columnIndex + (operation == TableEditOperation.InsertColumnRight ? 1 : 0);
                    if(table.Columns.Count > 0)
                    {
                        var source = table.Columns[columnIndex];
                        table.Columns.Insert(insertIndex, new TableColumn { Width = source.Width, Background = source.Background });
                    }
                    foreach(var item in rows)
                        item.Cells.Insert(insertIndex, CreateCell(item.Cells[columnIndex]));
                    destination = row.Cells[insertIndex];
                    break;

                case TableEditOperation.DeleteRow:
                    if(rows.Length == 1)
                    {
                        RemoveTable(table);
                        destination = null;
                        break;
                    }
                    int index = Array.IndexOf(rows, row);
                    destination = rows[index == rows.Length - 1 ? index - 1 : index + 1].Cells[columnIndex];
                    group.Rows.Remove(row);
                    if(group.Rows.Count == 0)
                        table.RowGroups.Remove(group);
                    break;

                case TableEditOperation.DeleteColumn:
                    if(row.Cells.Count == 1)
                    {
                        RemoveTable(table);
                        destination = null;
                        break;
                    }
                    destination = row.Cells[columnIndex == row.Cells.Count - 1 ? columnIndex - 1 : columnIndex + 1];
                    foreach(var item in rows)
                        item.Cells.RemoveAt(columnIndex);
                    if(table.Columns.Count > 0)
                        table.Columns.RemoveAt(columnIndex);
                    break;
            }

            if(destination is not null)
                Select(destination.ContentStart.GetInsertionPosition(LogicalDirection.Forward));
        }
        finally
        {
            editor.EndChange();
        }
        if(operation is TableEditOperation.DeleteRow or TableEditOperation.DeleteColumn)
            bookmarks?.RebuildIndexFromDocument(editor);
        editor.Focus();
        return true;
    }

    private TableCell? TryGetCell()
    {
        if(editor.IsReadOnly)
            return null;
        var cell = FindAncestor<TableCell>(editor.Selection.Start.Parent);
        if(cell is null || (!editor.Selection.IsEmpty &&
            !ReferenceEquals(cell, FindAncestor<TableCell>(editor.Selection.End.Parent))))
            return null;
        if(cell.Parent is not TableRow row || row.Parent is not TableRowGroup group ||
            group.Parent is not Table table ||
            table.Parent is not (FlowDocument or Section or ListItem or TableCell) ||
            !ReferenceEquals(FindAncestor<FlowDocument>(table), editor.Document))
            return null;

        int columns = row.Cells.Count;
        if(columns == 0 || (table.Columns.Count != 0 && table.Columns.Count != columns))
            return null;
        return table.RowGroups.SelectMany(item => item.Rows).All(item =>
            item.Cells.Count == columns && item.Cells.All(value => value.ColumnSpan == 1 && value.RowSpan == 1))
            ? cell : null;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while(current is not null)
        {
            if(current is T result)
                return result;
            current = (current as FrameworkContentElement)?.Parent;
        }
        return null;
    }

    private static TableCell CreateCell(TableCell source)
    {
        var result = new TableCell();
        CopyFormatting(source, result);
        result.Padding = source.Padding;
        result.BorderBrush = source.BorderBrush;
        result.BorderThickness = source.BorderThickness;
        result.TextAlignment = source.TextAlignment;
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        if(source.Blocks.FirstBlock is Paragraph original)
        {
            CopyFormatting(original, paragraph);
            paragraph.Margin = original.Margin;
            paragraph.TextAlignment = original.TextAlignment;
        }
        result.Blocks.Add(paragraph);
        return result;
    }

    private static void CopyFormatting(TextElement source, TextElement target)
    {
        target.FontFamily = source.FontFamily;
        target.FontSize = source.FontSize;
        target.FontWeight = source.FontWeight;
        target.FontStyle = source.FontStyle;
        target.FontStretch = source.FontStretch;
        target.Foreground = source.Foreground;
        target.Background = source.Background;
    }

    private static Table ReplaceTableShell(Table original)
    {
        // Table.Columns is outside WPF's text undo history. Keep the original
        // definitions on the old table and edit a new shell, so undo/redo also
        // restores widths. Move the live row groups to preserve cell content,
        // embedded objects and bookmark anchors without serializing them.
        var replacement = new Table();
        CopyLocalValues(original, replacement);
        replacement.Resources = original.Resources;
        foreach(var column in original.Columns)
        {
            var newColumn = new TableColumn();
            CopyLocalValues(column, newColumn);
            newColumn.Resources = column.Resources;
            replacement.Columns.Add(newColumn);
        }
        var walker = new FlowDocumentWalker();
        walker.InsertBefore(original, replacement);
        foreach(var group in original.RowGroups.ToArray())
        {
            original.RowGroups.Remove(group);
            replacement.RowGroups.Add(group);
        }
        walker.Remove(original);
        return replacement;
    }

    private static void CopyLocalValues(DependencyObject original, DependencyObject replacement)
    {
        var properties = original.GetLocalValueEnumerator();
        while(properties.MoveNext())
        {
            var property = properties.Current.Property;
            if(!property.ReadOnly)
                replacement.SetValue(property, original.GetValue(property));
        }
    }

    private void RemoveTable(Table table)
    {
        // A paragraph in the same block collection also gives an empty document
        // (or cell/list item containing the table) a valid editing position.
        var paragraph = new Paragraph();
        var walker = new FlowDocumentWalker();
        walker.InsertBefore(table, paragraph);
        walker.Remove(table);
        Select(paragraph.ContentStart);
    }

    private void Select(TextPointer position)
    {
        editor.CaretPosition = position;
        editor.Selection.Select(position, position);
    }

    private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs args)
    {
        if(args.UndoAction is System.Windows.Controls.UndoAction.Undo or System.Windows.Controls.UndoAction.Redo)
            bookmarks?.RebuildIndexFromDocument(editor);
    }

    public void Dispose() => editor.Service.TextChanged -= OnTextChanged;
}

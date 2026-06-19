using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using CryptoBook.Interfaces;

namespace CryptoBook.Services
{

    public sealed class FlowDocumentContentService: IFlowDocumentContentService
    {
        private readonly IFlowDocumentWalker _walker;

        public FlowDocumentContentService( IFlowDocumentWalker walker)
        {
            _walker = walker ?? throw new ArgumentNullException(nameof(walker));
        }

        public Paragraph AddParagraph( FlowDocument document, string text = "")
        {
            ArgumentNullException.ThrowIfNull(document);

            var paragraph = CreateParagraph(text);

            document.Blocks.Add(paragraph);

            return paragraph;
        }

        public Paragraph AddParagraphAfter( TextElement target, string text = "")
        {
            ArgumentNullException.ThrowIfNull(target);

            var paragraph = CreateParagraph(text);

            if(!_walker.InsertAfter(target, paragraph))
            {
                throw new InvalidOperationException(
                    "Не удалось вставить Paragraph после указанного элемента.");
            }

            return paragraph;
        }

        public Paragraph AddParagraphBefore( TextElement target, string text = "")
        {
            ArgumentNullException.ThrowIfNull(target);

            var paragraph = CreateParagraph(text);

            if(!_walker.InsertBefore(target, paragraph))
            {
                throw new InvalidOperationException(
                    "Не удалось вставить Paragraph перед указанным элементом.");
            }

            return paragraph;
        }

        public Run AddRun( Paragraph paragraph, string text)
        {
            ArgumentNullException.ThrowIfNull(paragraph);

            var run = new Run(text ?? string.Empty);

            paragraph.Inlines.Add(run);

            return run;
        }

        public Span AddSpan( Paragraph paragraph, string text = "")
        {
            ArgumentNullException.ThrowIfNull(paragraph);

            var span = new Span();

            if(!string.IsNullOrEmpty(text))
            {
                span.Inlines.Add(new Run(text));
            }

            paragraph.Inlines.Add(span);

            return span;
        }

        public InlineUIContainer AddInlineObject( Paragraph paragraph, UIElement element)
        {
            ArgumentNullException.ThrowIfNull(paragraph);
            ArgumentNullException.ThrowIfNull(element);

            var container = new InlineUIContainer(element);

            paragraph.Inlines.Add(container);

            return container;
        }

        public BlockUIContainer AddBlockObject( FlowDocument document, UIElement element)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(element);

            var container = new BlockUIContainer(element);

            document.Blocks.Add(container);

            return container;
        }

        public Section AddSection( FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            var section = new Section();

            document.Blocks.Add(section);

            return section;
        }

        public List AddList( FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            var list = new List();

            document.Blocks.Add(list);

            return list;
        }

        public ListItem AddListItem( List list, string text = "")
        {
            ArgumentNullException.ThrowIfNull(list);

            var item = new ListItem(
                CreateParagraph(text));

            list.ListItems.Add(item);

            return item;
        }

        public Table AddTable( FlowDocument document, int rows, int columns)
        {
            ArgumentNullException.ThrowIfNull(document);

            ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);

            ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);

            var table = new Table();

            for(var i = 0; i < columns; i++)
            {
                table.Columns.Add(new TableColumn());
            }

            var rowGroup = new TableRowGroup();

            for(var r = 0; r < rows; r++)
            {
                var row = new TableRow();

                for(var c = 0; c < columns; c++)
                {
                    var cell = new TableCell(
                        new Paragraph());

                    row.Cells.Add(cell);
                }

                rowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(rowGroup);
            document.Blocks.Add(table);

            return table;
        }

        public bool Remove(
            TextElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            return _walker.Remove(element);
        }

        public void Clear(
            FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            document.Blocks.Clear();
        }

        private static Paragraph CreateParagraph(
            string text)
        {
            var paragraph = new Paragraph();

            if(!string.IsNullOrEmpty(text))
            {
                paragraph.Inlines.Add(
                    new Run(text));
            }

            return paragraph;
        }
    }
}

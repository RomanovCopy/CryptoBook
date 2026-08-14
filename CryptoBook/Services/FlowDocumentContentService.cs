using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{

    public sealed class FlowDocumentContentService: IFlowDocumentContentService
    {
        private readonly IFlowDocumentWalker _walker;
        private readonly IParagraphFactory _paragraphFactory;

        public FlowDocumentContentService(
            IFlowDocumentWalker walker,
            IParagraphFactory paragraphFactory)
        {
            _walker = walker ?? throw new ArgumentNullException(nameof(walker));
            _paragraphFactory = paragraphFactory ??
                throw new ArgumentNullException(nameof(paragraphFactory));
        }

        public Paragraph CreateParagraph(string text = "")
        {
            IParagraphService paragraph = _paragraphFactory.Create();
            paragraph.Margin = new Thickness(0);
            paragraph.Element.ClearValue(Paragraph.LineHeightProperty);
            paragraph.LineStackingStrategy = LineStackingStrategy.MaxHeight;

            if(!string.IsNullOrEmpty(text))
                paragraph.Inlines.Add(new Run(text));

            return paragraph.Element;
        }

        public bool CanInsertParagraph(
            FrameworkContentElement target,
            DocumentStructureDropPosition position)
        {
            ArgumentNullException.ThrowIfNull(target);

            return position switch
            {
                DocumentStructureDropPosition.Before or
                DocumentStructureDropPosition.After =>
                    target is Block block && CanOwnBlocks(block.Parent),

                DocumentStructureDropPosition.Inside =>
                    CanOwnBlocks(target),

                _ => false
            };
        }

        public Paragraph InsertParagraph(
            FrameworkContentElement target,
            DocumentStructureDropPosition position,
            string text = "")
        {
            ArgumentNullException.ThrowIfNull(target);

            if(!CanInsertParagraph(target, position))
            {
                throw new InvalidOperationException(
                    "Невозможно вставить Paragraph в указанную позицию.");
            }

            Paragraph paragraph = CreateParagraph(text);
            bool inserted = position switch
            {
                DocumentStructureDropPosition.Before when target is TextElement element =>
                    _walker.InsertBefore(element, paragraph),
                DocumentStructureDropPosition.After when target is TextElement element =>
                    _walker.InsertAfter(element, paragraph),
                DocumentStructureDropPosition.Inside =>
                    InsertInside(target, paragraph),
                _ => false
            };

            if(!inserted)
            {
                throw new InvalidOperationException(
                    "Не удалось вставить Paragraph в указанную позицию.");
            }

            return paragraph;
        }

        public Paragraph AddParagraph( FlowDocument document, string text = "")
        {
            ArgumentNullException.ThrowIfNull(document);
            return InsertParagraph(
                document,
                DocumentStructureDropPosition.Inside,
                text);
        }

        public Paragraph AddParagraphAfter( TextElement target, string text = "")
        {
            ArgumentNullException.ThrowIfNull(target);

            return InsertParagraph(
                target,
                DocumentStructureDropPosition.After,
                text);
        }

        public Paragraph AddParagraphBefore( TextElement target, string text = "")
        {
            ArgumentNullException.ThrowIfNull(target);

            return InsertParagraph(
                target,
                DocumentStructureDropPosition.Before,
                text);
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
                        CreateParagraph());

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

        private static bool CanOwnBlocks(DependencyObject? owner) =>
            owner is FlowDocument or Section or ListItem or TableCell;

        private static bool InsertInside(
            FrameworkContentElement target,
            Paragraph paragraph)
        {
            switch(target)
            {
                case FlowDocument document:
                    document.Blocks.Add(paragraph);
                    return true;
                case Section section:
                    section.Blocks.Add(paragraph);
                    return true;
                case ListItem item:
                    item.Blocks.Add(paragraph);
                    return true;
                case TableCell cell:
                    cell.Blocks.Add(paragraph);
                    return true;
                default:
                    return false;
            }
        }
    }
}

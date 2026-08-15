using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using CryptoBook.Interfaces;


namespace CryptoBook.Infrastructure
{
    /// <summary>
    /// Обходит и изменяет логическое дерево FlowDocument, учитывая разные типы
    /// коллекций-владельцев WPF: Blocks, Inlines, ListItems, Rows и Cells.
    /// </summary>
    public sealed class FlowDocumentWalker: IFlowDocumentWalker
    {
        public IEnumerable<TextElement> Traverse(
            FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            foreach(var block in document.Blocks)
            {
                foreach(var item in TraverseBlock(block))
                {
                    yield return item;
                }
            }
        }

        public IEnumerable<T> Find<T>(FlowDocument document) where T : TextElement
        {
            return Traverse(document).OfType<T>();
        }

        public TextElement? GetParent(TextElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            return element.Parent as TextElement;
        }

        public bool Remove(TextElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            return element switch
            {
                Block block => RemoveBlock(block),
                Inline inline => RemoveInline(inline),
                ListItem item => RemoveListItem(item),
                TableRowGroup group => RemoveTableRowGroup(group),
                TableRow row => RemoveTableRow(row),
                TableCell cell => RemoveTableCell(cell),
                _ => false
            };
        }

        public bool InsertBefore(TextElement target, TextElement newElement)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(newElement);

            return InsertNear(target, newElement, before: true);
        }

        public bool InsertAfter(TextElement target, TextElement newElement)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(newElement);

            return InsertNear(target, newElement, before: false);
        }

        private static IEnumerable<TextElement> TraverseBlock(Block block)
        {
            // Родитель выдаётся раньше потомков, поэтому результат подходит как
            // для поиска, так и для последовательной обработки дерева сверху вниз.
            yield return block;

            switch(block)
            {
                case Paragraph paragraph:
                {
                    foreach(var inline in paragraph.Inlines)
                    {
                        foreach(var item in TraverseInline(inline))
                        {
                            yield return item;
                        }
                    }

                    break;
                }

                case Section section:
                {
                    foreach(var child in section.Blocks)
                    {
                        foreach(var item in TraverseBlock(child))
                        {
                            yield return item;
                        }
                    }

                    break;
                }

                case List list:
                {
                    foreach(var item in list.ListItems)
                    {
                        yield return item;

                        foreach(var child in item.Blocks)
                        {
                            foreach(var result in TraverseBlock(child))
                            {
                                yield return result;
                            }
                        }
                    }

                    break;
                }

                case Table table:
                {
                    foreach(var group in table.RowGroups)
                    {
                        yield return group;

                        foreach(var row in group.Rows)
                        {
                            yield return row;

                            foreach(var cell in row.Cells)
                            {
                                yield return cell;

                                foreach(var child in cell.Blocks)
                                {
                                    foreach(var result in TraverseBlock(child))
                                    {
                                        yield return result;
                                    }
                                }
                            }
                        }
                    }

                    break;
                }
            }
        }

        private static IEnumerable<TextElement> TraverseInline(Inline inline)
        {
            yield return inline;

            if(inline is Span span)
            {
                foreach(var child in span.Inlines)
                {
                    foreach(var item in TraverseInline(child))
                    {
                        yield return item;
                    }
                }
            }

            if(inline is AnchoredBlock anchoredBlock)
            {
                foreach(var block in anchoredBlock.Blocks)
                {
                    foreach(var item in TraverseBlock(block))
                    {
                        yield return item;
                    }
                }
            }
        }

        private static bool RemoveBlock(Block block)
        {
            switch(block.Parent)
            {
                case FlowDocument document:
                document.Blocks.Remove(block);
                return true;

                case Section section:
                section.Blocks.Remove(block);
                return true;

                case ListItem item:
                item.Blocks.Remove(block);
                return true;

                case TableCell cell:
                cell.Blocks.Remove(block);
                return true;

                default:
                return false;
            }
        }

        private static bool RemoveInline(Inline inline)
        {
            switch(inline.Parent)
            {
                case Paragraph paragraph:
                paragraph.Inlines.Remove(inline);
                return true;

                case Span span:
                span.Inlines.Remove(inline);
                return true;

                default:
                return false;
            }
        }

        private static bool RemoveListItem(ListItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if(item.Parent is not List list)
            {
                return false;
            }

            list.ListItems.Remove(item);
            return true;
        }

        private static bool RemoveTableRowGroup(TableRowGroup group)
        {
            if(group.Parent is not Table table)
            {
                return false;
            }

            table.RowGroups.Remove(group);
            return true;
        }

        private static bool RemoveTableRow(TableRow row)
        {
            if(row.Parent is not TableRowGroup group)
            {
                return false;
            }

            group.Rows.Remove(row);
            return true;
        }

        private static bool RemoveTableCell(TableCell cell)
        {
            if(cell.Parent is not TableRow row)
            {
                return false;
            }

            row.Cells.Remove(cell);
            return true;
        }

        private static bool InsertNear(TextElement target, TextElement newElement, bool before)
        {
            // WPF не предоставляет общей изменяемой коллекции TextElement.
            // Вставка допустима только между элементами одного структурного уровня.
            return (target, newElement) switch
            {
                (Block targetBlock, Block newBlock) =>
                    InsertBlockNear(targetBlock, newBlock, before),

                (Inline targetInline, Inline newInline) =>
                    InsertInlineNear(targetInline, newInline, before),

                (ListItem targetItem, ListItem newItem) =>
                    InsertListItemNear(targetItem, newItem, before),

                (TableRowGroup targetGroup, TableRowGroup newGroup) =>
                    InsertTableRowGroupNear(targetGroup, newGroup, before),

                (TableRow targetRow, TableRow newRow) =>
                    InsertTableRowNear(targetRow, newRow, before),

                (TableCell targetCell, TableCell newCell) =>
                    InsertTableCellNear(targetCell, newCell, before),

                _ => false
            };
        }

        private static bool InsertBlockNear(Block target, Block newBlock, bool before)
        {
            switch(target.Parent)
            {
                case FlowDocument document:
                if(before)
                {
                    document.Blocks.InsertBefore(target, newBlock);
                } else
                {
                    document.Blocks.InsertAfter(target, newBlock);
                }

                return true;

                case Section section:
                if(before)
                {
                    section.Blocks.InsertBefore(target, newBlock);
                } else
                {
                    section.Blocks.InsertAfter(target, newBlock);
                }

                return true;

                case ListItem item:
                if(before)
                {
                    item.Blocks.InsertBefore(target, newBlock);
                } else
                {
                    item.Blocks.InsertAfter(target, newBlock);
                }

                return true;

                case TableCell cell:
                if(before)
                {
                    cell.Blocks.InsertBefore(target, newBlock);
                } else
                {
                    cell.Blocks.InsertAfter(target, newBlock);
                }

                return true;

                default:
                return false;
            }
        }

        private static bool InsertInlineNear(Inline target, Inline newInline, bool before)
        {
            switch(target.Parent)
            {
                case Paragraph paragraph:
                if(before)
                {
                    paragraph.Inlines.InsertBefore(target, newInline);
                } else
                {
                    paragraph.Inlines.InsertAfter(target, newInline);
                }

                return true;

                case Span span:
                if(before)
                {
                    span.Inlines.InsertBefore(target, newInline);
                } else
                {
                    span.Inlines.InsertAfter(target, newInline);
                }

                return true;

                default:
                return false;
            }
        }

        private static bool InsertListItemNear(ListItem target, ListItem newItem, bool before)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(newItem);

            if(target.Parent is not List list)
            {
                return false;
            }

            if(before)
            {
                list.ListItems.InsertBefore(target, newItem);
            } else
            {
                list.ListItems.InsertAfter(target, newItem);
            }

            return true;
        }

        private static bool InsertTableRowGroupNear(
            TableRowGroup target,
            TableRowGroup newGroup,
            bool before)
        {
            if(target.Parent is not Table table)
            {
                return false;
            }

            var index = table.RowGroups.IndexOf(target);

            if(index < 0)
            {
                return false;
            }

            if(!before)
            {
                index++;
            }

            table.RowGroups.Insert(index, newGroup);
            return true;
        }

        private static bool InsertTableRowNear(
            TableRow target,
            TableRow newRow,
            bool before)
        {
            if(target.Parent is not TableRowGroup group)
            {
                return false;
            }

            var index = group.Rows.IndexOf(target);

            if(index < 0)
            {
                return false;
            }

            if(!before)
            {
                index++;
            }

            group.Rows.Insert(index, newRow);
            return true;
        }

        private static bool InsertTableCellNear(
            TableCell target,
            TableCell newCell,
            bool before)
        {
            if(target.Parent is not TableRow row)
            {
                return false;
            }

            var index = row.Cells.IndexOf(target);

            if(index < 0)
            {
                return false;
            }

            if(!before)
            {
                index++;
            }

            row.Cells.Insert(index, newCell);
            return true;
        }
    }
}

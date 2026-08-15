using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    /// <summary>
    /// Перемещает структурные элементы между совместимыми коллекциями WPF.
    /// Сервис сохраняет сам объект, поэтому локальное форматирование, вложенное
    /// содержимое, изображения и якоря закладок не сериализуются и не клонируются.
    /// </summary>
    public sealed class FlowDocumentMoveService: IFlowDocumentMoveService
    {
        private readonly IParagraphFactory paragraphFactory;

        public FlowDocumentMoveService(IParagraphFactory paragraphFactory)
        {
            this.paragraphFactory = paragraphFactory
                ?? throw new ArgumentNullException(nameof(paragraphFactory));
        }

        public bool CanMove(
            FlowDocument document,
            TextElement source,
            FrameworkContentElement target,
            DocumentStructureDropPosition position)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            return TryResolveMove(
                document,
                source,
                target,
                position,
                out _,
                out _);
        }

        public bool Move(
            FlowDocument document,
            TextElement source,
            FrameworkContentElement target,
            DocumentStructureDropPosition position)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            if(!TryResolveMove(
                document,
                source,
                target,
                position,
                out SourceLocation sourceLocation,
                out MoveDestination destination))
            {
                return false;
            }

            RemoveFromOwner(source, sourceLocation.Owner);
            try
            {
                InsertIntoDestination(source, destination);
            }
            catch
            {
                RestoreSource(source, sourceLocation);
                throw;
            }

            if(!ReferenceEquals(sourceLocation.Owner, destination.Owner))
                EnsureEditableOwner(sourceLocation.Owner);
            return true;
        }

        private static bool TryResolveMove(
            FlowDocument document,
            TextElement source,
            FrameworkContentElement target,
            DocumentStructureDropPosition position,
            out SourceLocation sourceLocation,
            out MoveDestination destination)
        {
            sourceLocation = null!;
            destination = null!;

            if(position != DocumentStructureDropPosition.Before &&
               position != DocumentStructureDropPosition.Inside &&
               position != DocumentStructureDropPosition.After ||
               !IsAttached(document, source) ||
               !IsAttached(document, target) ||
               ReferenceEquals(source, target) ||
               IsAncestorOf(source, target) ||
               !TryCaptureSource(source, out sourceLocation) ||
               !TryResolveDestination(
                   source,
                   target,
                   position,
                   out destination))
            {
                return false;
            }

            return !IsNoOp(source, sourceLocation.Owner, destination);
        }

        private static bool TryCaptureSource(
            TextElement source,
            out SourceLocation location)
        {
            location = null!;
            switch(source)
            {
                case Block block when IsSupportedBlockOwner(block.Parent):
                    location = new SourceLocation(
                        block.Parent!,
                        block.PreviousBlock,
                        block.NextBlock);
                    return true;

                case ListItem item
                    when item.Parent is System.Windows.Documents.List list:
                {
                    ListItem[] items = list.ListItems.Cast<ListItem>().ToArray();
                    int index = Array.IndexOf(items, item);
                    if(index < 0)
                        return false;
                    location = new SourceLocation(
                        list,
                        index > 0 ? items[index - 1] : null,
                        index + 1 < items.Length ? items[index + 1] : null);
                    return true;
                }

                case TableRow row when row.Parent is TableRowGroup group:
                {
                    int index = group.Rows.IndexOf(row);
                    if(index < 0)
                        return false;
                    location = new SourceLocation(
                        group,
                        index > 0 ? group.Rows[index - 1] : null,
                        index + 1 < group.Rows.Count
                            ? group.Rows[index + 1]
                            : null);
                    return true;
                }

                default:
                    return false;
            }
        }

        private static bool TryResolveDestination(
            TextElement source,
            FrameworkContentElement target,
            DocumentStructureDropPosition position,
            out MoveDestination destination)
        {
            destination = null!;
            switch(source)
            {
                case Block:
                    return TryResolveBlockDestination(
                        target,
                        position,
                        out destination);

                case ListItem:
                    return TryResolveListItemDestination(
                        target,
                        position,
                        out destination);

                case TableRow:
                    return TryResolveTableRowDestination(
                        target,
                        position,
                        out destination);

                default:
                    return false;
            }
        }

        private static bool TryResolveBlockDestination(
            FrameworkContentElement target,
            DocumentStructureDropPosition position,
            out MoveDestination destination)
        {
            destination = null!;
            if(position == DocumentStructureDropPosition.Inside)
            {
                if(!IsSupportedBlockOwner(target))
                    return false;
                destination = new MoveDestination(target, null, false);
                return true;
            }

            if(target is not Block targetBlock ||
               !IsSupportedBlockOwner(targetBlock.Parent))
            {
                return false;
            }

            destination = new MoveDestination(
                targetBlock.Parent!,
                targetBlock,
                position == DocumentStructureDropPosition.Before);
            return true;
        }

        private static bool TryResolveListItemDestination(
            FrameworkContentElement target,
            DocumentStructureDropPosition position,
            out MoveDestination destination)
        {
            destination = null!;
            if(position == DocumentStructureDropPosition.Inside)
            {
                if(target is not System.Windows.Documents.List list)
                    return false;
                destination = new MoveDestination(list, null, false);
                return true;
            }

            if(target is not ListItem targetItem ||
               targetItem.Parent is not System.Windows.Documents.List owner)
            {
                return false;
            }

            destination = new MoveDestination(
                owner,
                targetItem,
                position == DocumentStructureDropPosition.Before);
            return true;
        }

        private static bool TryResolveTableRowDestination(
            FrameworkContentElement target,
            DocumentStructureDropPosition position,
            out MoveDestination destination)
        {
            destination = null!;
            if(position == DocumentStructureDropPosition.Inside)
            {
                if(target is not TableRowGroup group)
                    return false;
                destination = new MoveDestination(group, null, false);
                return true;
            }

            if(target is not TableRow targetRow ||
               targetRow.Parent is not TableRowGroup owner)
            {
                return false;
            }

            destination = new MoveDestination(
                owner,
                targetRow,
                position == DocumentStructureDropPosition.Before);
            return true;
        }

        private static bool IsNoOp(
            TextElement source,
            object sourceOwner,
            MoveDestination destination)
        {
            if(!ReferenceEquals(sourceOwner, destination.Owner))
                return false;

            TextElement[] elements = GetOwnerElements(sourceOwner).ToArray();
            int sourceIndex = Array.IndexOf(elements, source);
            if(sourceIndex < 0)
                return true;

            int insertionIndex;
            if(destination.Anchor is null)
            {
                insertionIndex = elements.Length;
            }
            else
            {
                int anchorIndex = Array.IndexOf(elements, destination.Anchor);
                if(anchorIndex < 0)
                    return true;
                insertionIndex = anchorIndex + (destination.Before ? 0 : 1);
            }

            if(sourceIndex < insertionIndex)
                insertionIndex--;
            return sourceIndex == insertionIndex;
        }

        private static IEnumerable<TextElement> GetOwnerElements(object owner) =>
            owner switch
            {
                FlowDocument document => document.Blocks.Cast<Block>(),
                Section section => section.Blocks.Cast<Block>(),
                ListItem item => item.Blocks.Cast<Block>(),
                TableCell cell => cell.Blocks.Cast<Block>(),
                System.Windows.Documents.List list =>
                    list.ListItems.Cast<ListItem>(),
                TableRowGroup group => group.Rows.Cast<TableRow>(),
                _ => []
            };

        private static void RemoveFromOwner(
            TextElement source,
            object owner)
        {
            switch(source, owner)
            {
                case (Block block, FlowDocument document):
                    document.Blocks.Remove(block);
                    break;
                case (Block block, Section section):
                    section.Blocks.Remove(block);
                    break;
                case (Block block, ListItem item):
                    item.Blocks.Remove(block);
                    break;
                case (Block block, TableCell cell):
                    cell.Blocks.Remove(block);
                    break;
                case (ListItem item, System.Windows.Documents.List list):
                    list.ListItems.Remove(item);
                    break;
                case (TableRow row, TableRowGroup group):
                    group.Rows.Remove(row);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Источник больше не принадлежит ожидаемому контейнеру.");
            }
        }

        private static void InsertIntoDestination(
            TextElement source,
            MoveDestination destination)
        {
            switch(source, destination.Owner)
            {
                case (Block block, FlowDocument document):
                    InsertBlock(document.Blocks, block, destination);
                    break;
                case (Block block, Section section):
                    InsertBlock(section.Blocks, block, destination);
                    break;
                case (Block block, ListItem item):
                    InsertBlock(item.Blocks, block, destination);
                    break;
                case (Block block, TableCell cell):
                    InsertBlock(cell.Blocks, block, destination);
                    break;
                case (ListItem item, System.Windows.Documents.List list):
                    InsertListItem(list, item, destination);
                    break;
                case (TableRow row, TableRowGroup group):
                    InsertTableRow(group, row, destination);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Назначение больше не принимает перемещаемый элемент.");
            }
        }

        private static void InsertBlock(
            BlockCollection blocks,
            Block source,
            MoveDestination destination)
        {
            if(destination.Anchor is not Block anchor)
            {
                blocks.Add(source);
                return;
            }

            if(destination.Before)
                blocks.InsertBefore(anchor, source);
            else
                blocks.InsertAfter(anchor, source);
        }

        private static void InsertListItem(
            System.Windows.Documents.List list,
            ListItem source,
            MoveDestination destination)
        {
            if(destination.Anchor is not ListItem anchor)
            {
                list.ListItems.Add(source);
                return;
            }

            if(destination.Before)
                list.ListItems.InsertBefore(anchor, source);
            else
                list.ListItems.InsertAfter(anchor, source);
        }

        private static void InsertTableRow(
            TableRowGroup group,
            TableRow source,
            MoveDestination destination)
        {
            if(destination.Anchor is not TableRow anchor)
            {
                group.Rows.Add(source);
                return;
            }

            int index = group.Rows.IndexOf(anchor);
            if(index < 0)
            {
                throw new InvalidOperationException(
                    "Строка назначения больше не принадлежит таблице.");
            }

            group.Rows.Insert(index + (destination.Before ? 0 : 1), source);
        }

        private static void RestoreSource(
            TextElement source,
            SourceLocation location)
        {
            if(source.Parent is not null)
                RemoveFromOwner(source, source.Parent);

            TextElement? anchor = location.Next?.Parent == location.Owner
                ? location.Next
                : location.Previous?.Parent == location.Owner
                    ? location.Previous
                    : null;
            bool before = ReferenceEquals(anchor, location.Next);
            InsertIntoDestination(
                source,
                new MoveDestination(location.Owner, anchor, before));
        }

        private void EnsureEditableOwner(object owner)
        {
            switch(owner)
            {
                case FlowDocument document when document.Blocks.Count == 0:
                    document.Blocks.Add(CreateParagraph());
                    break;
                case Section section when section.Blocks.Count == 0:
                    section.Blocks.Add(CreateParagraph());
                    break;
                case ListItem item when item.Blocks.Count == 0:
                    item.Blocks.Add(CreateParagraph());
                    break;
                case TableCell cell when cell.Blocks.Count == 0:
                    cell.Blocks.Add(CreateParagraph());
                    break;
                case System.Windows.Documents.List list
                    when list.ListItems.Count == 0:
                    RemoveEmptyBlock(list);
                    break;
                case TableRowGroup group when group.Rows.Count == 0:
                    RemoveEmptyRowGroup(group);
                    break;
            }
        }

        private void RemoveEmptyBlock(Block block)
        {
            object? owner = block.Parent;
            if(owner is null)
                return;
            RemoveFromOwner(block, owner);
            EnsureEditableOwner(owner);
        }

        private void RemoveEmptyRowGroup(TableRowGroup group)
        {
            if(group.Parent is not Table table)
                return;

            table.RowGroups.Remove(group);
            if(table.RowGroups.Count == 0)
                RemoveEmptyBlock(table);
        }

        private Paragraph CreateParagraph()
        {
            IParagraphService paragraph = paragraphFactory.Create();
            paragraph.Margin = new Thickness(0);
            paragraph.Element.ClearValue(Paragraph.LineHeightProperty);
            paragraph.LineStackingStrategy = LineStackingStrategy.MaxHeight;
            return paragraph.Element;
        }

        private static bool IsSupportedBlockOwner(object? owner) =>
            owner is FlowDocument or Section or ListItem or TableCell;

        private static bool IsAttached(
            FlowDocument document,
            FrameworkContentElement element)
        {
            FrameworkContentElement? current = element;
            while(current is not null)
            {
                if(ReferenceEquals(current, document))
                    return true;
                current = current.Parent as FrameworkContentElement;
            }

            return false;
        }

        private static bool IsAncestorOf(
            FrameworkContentElement source,
            FrameworkContentElement target)
        {
            FrameworkContentElement? current = target.Parent
                as FrameworkContentElement;
            while(current is not null)
            {
                if(ReferenceEquals(current, source))
                    return true;
                current = current.Parent as FrameworkContentElement;
            }

            return false;
        }

        private sealed record SourceLocation(
            object Owner,
            TextElement? Previous,
            TextElement? Next);

        private sealed record MoveDestination(
            object Owner,
            TextElement? Anchor,
            bool Before);
    }
}

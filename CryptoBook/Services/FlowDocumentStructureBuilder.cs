using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Services
{
    /// <summary>
    /// Создаёт компактный снимок логической структуры FlowDocument.
    /// В обычном режиме узлы чистого форматирования пропускаются, но их
    /// структурно значимые потомки поднимаются к ближайшему видимому предку.
    /// </summary>
    public sealed class FlowDocumentStructureBuilder:
        IFlowDocumentStructureBuilder
    {
        private const int PreviewLength = 64;
        private const string BookmarkNamePrefix = "Bookmark_";
        private const string BookmarkMetadataPrefix = "CryptoBook.Bookmark:";
        private const string CaretAnchorText = "\u200B";
        private const string MoveAnchorText = "\u2060";

        public DocumentStructureNode Build(
            FlowDocument document,
            bool includeTextElements)
        {
            ArgumentNullException.ThrowIfNull(document);

            IReadOnlyList<DocumentStructureNode> children = BuildChildren(
                document,
                "FlowDocument",
                includeTextElements);
            return new DocumentStructureNode(
                document,
                "FlowDocument",
                nameof(FlowDocument),
                document.Blocks.Count.ToString(CultureInfo.CurrentCulture),
                "\uE8FD",
                canDelete: false,
                children);
        }

        private static IReadOnlyList<DocumentStructureNode> BuildChildren(
            FrameworkContentElement parent,
            string parentPath,
            bool includeTextElements)
        {
            var result = new List<DocumentStructureNode>();
            var children = new List<(FrameworkContentElement Source, string Path)>();
            var typeIndexes = new Dictionary<string, int>(
                StringComparer.Ordinal);

            foreach(FrameworkContentElement child in EnumerateChildren(parent))
            {
                string typeName = GetTypeName(child);
                typeIndexes.TryGetValue(typeName, out int index);
                index++;
                typeIndexes[typeName] = index;
                string path = $"{parentPath}/{typeName}[{index}]";

                children.Add((child, path));
            }

            for(int childIndex = 0; childIndex < children.Count; childIndex++)
            {
                (FrameworkContentElement child, string path) = children[childIndex];

                if(includeTextElements &&
                   child is Run firstRun &&
                   !IsCaretAnchor(firstRun))
                {
                    var runs = new List<Run> { firstRun };
                    int nextIndex = childIndex + 1;
                    while(nextIndex < children.Count &&
                          children[nextIndex].Source is Run nextRun &&
                          !IsCaretAnchor(nextRun) &&
                          InlineService.HaveEquivalentRunProperties(
                              runs[^1],
                              nextRun))
                    {
                        runs.Add(nextRun);
                        nextIndex++;
                    }

                    if(runs.Count > 1)
                    {
                        result.Add(CreateRunGroupNode(runs, path));
                        childIndex = nextIndex - 1;
                        continue;
                    }
                }

                foreach(DocumentStructureNode node in BuildNode(
                    child,
                    path,
                    includeTextElements))
                {
                    result.Add(node);
                }
            }

            return result;
        }

        private static DocumentStructureNode CreateRunGroupNode(
            IReadOnlyList<Run> runs,
            string path)
        {
            Run firstRun = runs[0];
            string text = string.Concat(runs.Select(run => run.Text));
            return new DocumentStructureNode(
                firstRun,
                path,
                nameof(Run),
                Quote(CreatePreview(text)),
                GetGlyph(firstRun),
                canDelete: true,
                [],
                runs.Cast<FrameworkContentElement>().ToArray());
        }

        private static IEnumerable<DocumentStructureNode> BuildNode(
            FrameworkContentElement source,
            string path,
            bool includeTextElements)
        {
            IReadOnlyList<DocumentStructureNode> children = BuildChildren(
                source,
                path,
                includeTextElements);

            if(IsCaretAnchor(source))
                return children;

            if(!includeTextElements && !IsStructuralElement(source))
                return children;

            bool isProtectedBookmark = IsBookmark(source);
            return
            [
                new DocumentStructureNode(
                    source,
                    path,
                    GetTypeName(source),
                    CreateSummary(source, isProtectedBookmark),
                    GetGlyph(source),
                    source is TextElement && !isProtectedBookmark,
                    children)
            ];
        }

        private static IEnumerable<FrameworkContentElement> EnumerateChildren(
            FrameworkContentElement source)
        {
            switch(source)
            {
                case FlowDocument document:
                    foreach(Block block in document.Blocks)
                        yield return block;
                    break;

                case Section section:
                    foreach(Block block in section.Blocks)
                        yield return block;
                    break;

                case Paragraph paragraph:
                    foreach(Inline inline in paragraph.Inlines)
                        yield return inline;
                    break;

                case System.Windows.Documents.List list:
                    foreach(ListItem item in list.ListItems)
                        yield return item;
                    break;

                case ListItem item:
                    foreach(Block block in item.Blocks)
                        yield return block;
                    break;

                case Table table:
                    foreach(TableRowGroup group in table.RowGroups)
                        yield return group;
                    break;

                case TableRowGroup group:
                    foreach(TableRow row in group.Rows)
                        yield return row;
                    break;

                case TableRow row:
                    foreach(TableCell cell in row.Cells)
                        yield return cell;
                    break;

                case TableCell cell:
                    foreach(Block block in cell.Blocks)
                        yield return block;
                    break;

                case Span span:
                    foreach(Inline inline in span.Inlines)
                        yield return inline;
                    break;

                case AnchoredBlock anchoredBlock:
                    foreach(Block block in anchoredBlock.Blocks)
                        yield return block;
                    break;
            }
        }

        private static bool IsStructuralElement(
            FrameworkContentElement source) =>
            source is Block or
            ListItem or
            TableRowGroup or
            TableRow or
            TableCell or
            Hyperlink or
            AnchoredBlock or
            InlineUIContainer;

        private static bool IsBookmark(FrameworkContentElement source) =>
            source is Span span &&
            (span.Name.StartsWith(
                 BookmarkNamePrefix,
                 StringComparison.Ordinal) ||
             span.Tag is string metadata &&
             metadata.StartsWith(
                 BookmarkMetadataPrefix,
                 StringComparison.Ordinal));

        private static bool IsCaretAnchor(FrameworkContentElement source) =>
            source is Run run &&
            (string.Equals(run.Text, CaretAnchorText, StringComparison.Ordinal) ||
             string.Equals(run.Text, MoveAnchorText, StringComparison.Ordinal));

        private static string CreateSummary(
            FrameworkContentElement source,
            bool isProtectedBookmark)
        {
            if(isProtectedBookmark)
                return "[bookmark]";

            return source switch
            {
                Run run => Quote(CreatePreview(run.Text)),
                Paragraph paragraph => Quote(CreatePreview(
                    new TextRange(
                        paragraph.ContentStart,
                        paragraph.ContentEnd).Text)),
                Hyperlink hyperlink when hyperlink.NavigateUri is not null =>
                    hyperlink.NavigateUri.ToString(),
                System.Windows.Documents.List list =>
                    list.ListItems.Count.ToString(
                        CultureInfo.CurrentCulture),
                Table table => CreateTableSummary(table),
                TableRow row => row.Cells.Count.ToString(
                    CultureInfo.CurrentCulture),
                InlineUIContainer inlineContainer =>
                    CreateUiSummary(inlineContainer.Child),
                BlockUIContainer blockContainer =>
                    CreateUiSummary(blockContainer.Child),
                AnchoredBlock anchoredBlock =>
                    FindImage(anchoredBlock) is WpfImage image
                        ? CreateUiSummary(image)
                        : string.Empty,
                _ => string.Empty
            };
        }

        private static string CreateTableSummary(Table table)
        {
            int rows = table.RowGroups.Sum(group => group.Rows.Count);
            int columns = Math.Max(
                table.Columns.Count,
                table.RowGroups
                .SelectMany(group => group.Rows)
                .Select(row => row.Cells.Count)
                .DefaultIfEmpty(0)
                .Max());
            return $"{rows} × {columns}";
        }

        private static string CreateUiSummary(UIElement? child)
        {
            if(child is not WpfImage image)
                return child?.GetType().Name ?? string.Empty;

            if(image.Source is BitmapSource bitmap &&
               bitmap.PixelWidth > 0 &&
               bitmap.PixelHeight > 0)
            {
                return $"Image {bitmap.PixelWidth} × {bitmap.PixelHeight}";
            }

            return "Image";
        }

        private static WpfImage? FindImage(AnchoredBlock anchoredBlock)
        {
            foreach(Block block in anchoredBlock.Blocks)
            {
                if(block is BlockUIContainer { Child: WpfImage image })
                    return image;

                if(block is Section section)
                {
                    WpfImage? nested = FindImage(section.Blocks);
                    if(nested is not null)
                        return nested;
                }
            }

            return null;
        }

        private static WpfImage? FindImage(BlockCollection blocks)
        {
            foreach(Block block in blocks)
            {
                if(block is BlockUIContainer { Child: WpfImage image })
                    return image;
                if(block is Section section)
                {
                    WpfImage? nested = FindImage(section.Blocks);
                    if(nested is not null)
                        return nested;
                }
            }

            return null;
        }

        private static string GetGlyph(FrameworkContentElement source) =>
            source switch
            {
                FlowDocument => "\uE8FD",
                Table or TableRowGroup or TableRow or TableCell => "\uE80A",
                System.Windows.Documents.List or ListItem => "\uEA37",
                Hyperlink => "\uE71B",
                InlineUIContainer or BlockUIContainer or AnchoredBlock => "\uEB9F",
                Run or Span or LineBreak => "\uE8D2",
                _ => "\uE8A5"
            };

        private static string GetTypeName(FrameworkContentElement source) =>
            source switch
            {
                FlowDocument => nameof(FlowDocument),
                Paragraph => nameof(Paragraph),
                Section => nameof(Section),
                System.Windows.Documents.List => nameof(System.Windows.Documents.List),
                ListItem => nameof(ListItem),
                Table => nameof(Table),
                TableRowGroup => nameof(TableRowGroup),
                TableRow => nameof(TableRow),
                TableCell => nameof(TableCell),
                Hyperlink => nameof(Hyperlink),
                Figure => nameof(Figure),
                Floater => nameof(Floater),
                InlineUIContainer => nameof(InlineUIContainer),
                BlockUIContainer => nameof(BlockUIContainer),
                Run => nameof(Run),
                Span => nameof(Span),
                LineBreak => nameof(LineBreak),
                _ => source.GetType().Name
            };

        private static string CreatePreview(string? value)
        {
            if(string.IsNullOrEmpty(value))
                return string.Empty;

            var result = new StringBuilder();
            bool previousWasSpace = false;
            foreach(char character in value)
            {
                UnicodeCategory category = char.GetUnicodeCategory(character);
                if(category is UnicodeCategory.Control or
                   UnicodeCategory.Format)
                {
                    continue;
                }

                if(char.IsWhiteSpace(character))
                {
                    if(result.Length > 0 && !previousWasSpace)
                        result.Append(' ');
                    previousWasSpace = true;
                    continue;
                }

                result.Append(character);
                previousWasSpace = false;
                if(result.Length >= PreviewLength)
                    break;
            }

            string preview = result.ToString().Trim();
            return preview.Length == PreviewLength
                ? preview + "…"
                : preview;
        }

        private static string Quote(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : $"“{value}”";
    }
}

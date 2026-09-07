using CryptoBook.Interfaces;

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using WpfBlock = System.Windows.Documents.Block;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfImage = System.Windows.Controls.Image;
using WpfInline = System.Windows.Documents.Inline;
using WpfList = System.Windows.Documents.List;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;

namespace CryptoBook.Services
{
    /// <summary>
    /// One-way Markdown renderer. It never mutates or reads back from the
    /// resulting FlowDocument.
    /// </summary>
    public sealed class MarkdownFlowDocumentRenderer:
        IMarkdownFlowDocumentRenderer
    {
        private const long MaximumLocalImageBytes = 25L * 1024 * 1024;
        private static readonly MarkdownPipeline Pipeline =
            new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .DisableHtml()
                .Build();

        public FlowDocument Render(
            string markdown,
            string? markdownFilePath)
        {
            var document = new FlowDocument();
            DocumentPageLayout.Apply(document);
            document.SetResourceReference(
                TextElement.ForegroundProperty,
                "CurrentWindowForeground");
            document.SetResourceReference(
                FlowDocument.BackgroundProperty,
                "CurrentDocumentBackground");
            document.FontFamily = new WpfFontFamily("Segoe UI");
            document.FontSize = 15;
            document.LineHeight = double.NaN;

            MarkdownDocument parsed = Markdown.Parse(
                markdown ?? string.Empty,
                Pipeline);
            var context = new RenderContext(GetSourceDirectory(
                markdownFilePath));
            foreach(MdBlock block in parsed)
            {
                foreach(WpfBlock rendered in RenderBlock(block, context))
                    document.Blocks.Add(rendered);
            }

            if(document.Blocks.Count == 0)
                document.Blocks.Add(new Paragraph());
            return document;
        }

        private static IEnumerable<WpfBlock> RenderBlock(
            MdBlock block,
            RenderContext context)
        {
            switch(block)
            {
                case HeadingBlock heading:
                    yield return RenderHeading(heading, context);
                    yield break;

                case ParagraphBlock paragraph:
                    yield return RenderParagraph(paragraph, context);
                    yield break;

                case QuoteBlock quote:
                    yield return RenderQuote(quote, context);
                    yield break;

                case ListBlock list:
                    yield return RenderList(list, context);
                    yield break;

                case ThematicBreakBlock:
                    var separator = new Paragraph
                    {
                        Margin = new Thickness(0, 12, 0, 12),
                        BorderThickness = new Thickness(0, 0, 0, 1)
                    };
                    separator.SetResourceReference(
                        WpfBlock.BorderBrushProperty,
                        "CurrentBorderColor");
                    yield return separator;
                    yield break;

                case MdTable table:
                    yield return RenderTable(table, context);
                    yield break;

                case FencedCodeBlock fenced:
                    yield return RenderCodeBlock(
                        fenced.Lines.ToString(),
                        fenced.Info);
                    yield break;

                case CodeBlock code:
                    yield return RenderCodeBlock(
                        code.Lines.ToString(),
                        null);
                    yield break;

                case HtmlBlock html:
                    // Raw HTML is deliberately shown as inert source text.
                    yield return RenderCodeBlock(
                        html.Lines.ToString(),
                        "html");
                    yield break;

                case LeafBlock leaf when leaf.Inline is not null:
                    yield return RenderLeaf(leaf, context);
                    yield break;

                case ContainerBlock container:
                    foreach(MdBlock child in container)
                    {
                        foreach(WpfBlock rendered in RenderBlock(child, context))
                            yield return rendered;
                    }
                    yield break;
            }
        }

        private static Paragraph RenderHeading(
            HeadingBlock heading,
            RenderContext context)
        {
            double[] sizes = [30, 26, 22, 19, 17, 15];
            var paragraph = new Paragraph
            {
                FontSize = sizes[Math.Clamp(heading.Level - 1, 0, sizes.Length - 1)],
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, heading.Level <= 2 ? 18 : 12, 0, 7),
                KeepWithNext = true
            };
            AppendChildren(paragraph.Inlines, heading.Inline, context);
            return paragraph;
        }

        private static Paragraph RenderParagraph(
            ParagraphBlock source,
            RenderContext context)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 4, 0, 8)
            };
            AppendChildren(paragraph.Inlines, source.Inline, context);
            return paragraph;
        }

        private static Paragraph RenderLeaf(
            LeafBlock source,
            RenderContext context)
        {
            var paragraph = new Paragraph();
            AppendChildren(paragraph.Inlines, source.Inline, context);
            return paragraph;
        }

        private static Section RenderQuote(
            QuoteBlock quote,
            RenderContext context)
        {
            var section = new Section
            {
                Margin = new Thickness(0, 7, 0, 10),
                Padding = new Thickness(14, 5, 8, 5),
                BorderThickness = new Thickness(4, 0, 0, 0)
            };
            ApplyBlockSurfaceTheme(section);
            foreach(MdBlock child in quote)
            {
                foreach(WpfBlock rendered in RenderBlock(child, context))
                    section.Blocks.Add(rendered);
            }
            if(section.Blocks.Count == 0)
                section.Blocks.Add(new Paragraph());
            return section;
        }

        private static WpfList RenderList(
            ListBlock source,
            RenderContext context)
        {
            var list = new WpfList
            {
                MarkerStyle = source.IsOrdered
                    ? TextMarkerStyle.Decimal
                    : TextMarkerStyle.Disc,
                Margin = new Thickness(16, 5, 0, 9),
                Padding = new Thickness(14, 0, 0, 0)
            };
            if(source.IsOrdered &&
               int.TryParse(source.OrderedStart, out int start))
            {
                list.StartIndex = start;
            }

            foreach(ListItemBlock sourceItem in source.OfType<ListItemBlock>())
            {
                var item = new ListItem();
                foreach(MdBlock child in sourceItem)
                {
                    foreach(WpfBlock rendered in RenderBlock(child, context))
                        item.Blocks.Add(rendered);
                }
                if(item.Blocks.Count == 0)
                    item.Blocks.Add(new Paragraph());
                list.ListItems.Add(item);
            }
            return list;
        }

        private static WpfBlock RenderCodeBlock(
            string code,
            string? language)
        {
            var paragraph = new Paragraph
            {
                FontFamily = new WpfFontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 8, 0, 12)
            };
            ApplyBlockSurfaceTheme(paragraph);
            if(!string.IsNullOrWhiteSpace(language))
            {
                var languageLabel = new Run(language.Trim())
                {
                    FontFamily = new WpfFontFamily("Segoe UI"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold
                };
                languageLabel.SetResourceReference(
                    TextElement.ForegroundProperty,
                    "CurrentMutedForeground");
                paragraph.Inlines.Add(languageLabel);
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new Run(code.TrimEnd('\r', '\n')));
            return paragraph;
        }

        private static WpfTable RenderTable(
            MdTable source,
            RenderContext context)
        {
            var table = new WpfTable
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 8, 0, 12),
                BorderThickness = new Thickness(1)
            };
            table.SetResourceReference(
                WpfBlock.BorderBrushProperty,
                "CurrentBorderColor");
            var rowGroup = new TableRowGroup();
            table.RowGroups.Add(rowGroup);

            foreach(MdTableRow sourceRow in source.OfType<MdTableRow>())
            {
                var row = new WpfTableRow();
                rowGroup.Rows.Add(row);
                foreach(MdTableCell sourceCell in sourceRow.OfType<MdTableCell>())
                {
                    var cell = new WpfTableCell
                    {
                        Padding = new Thickness(8, 5, 8, 5),
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        FontWeight = sourceRow.IsHeader
                            ? FontWeights.SemiBold
                            : FontWeights.Normal,
                        ColumnSpan = Math.Max(1, sourceCell.ColumnSpan),
                        RowSpan = Math.Max(1, sourceCell.RowSpan)
                    };
                    cell.SetResourceReference(
                        WpfTableCell.BorderBrushProperty,
                        "CurrentBorderColor");
                    if(sourceRow.IsHeader)
                    {
                        cell.SetResourceReference(
                            TextElement.BackgroundProperty,
                            "CurrentControlBackground");
                    }
                    foreach(MdBlock child in sourceCell)
                    {
                        foreach(WpfBlock rendered in RenderBlock(child, context))
                            cell.Blocks.Add(rendered);
                    }
                    if(cell.Blocks.Count == 0)
                        cell.Blocks.Add(new Paragraph());
                    row.Cells.Add(cell);
                }
            }
            return table;
        }

        private static void AppendChildren(
            InlineCollection target,
            ContainerInline? source,
            RenderContext context)
        {
            if(source is null)
                return;

            for(MdInline? child = source.FirstChild;
                child is not null;
                child = child.NextSibling)
            {
                AppendInline(target, child, context);
            }
        }

        private static void AppendInline(
            InlineCollection target,
            MdInline source,
            RenderContext context)
        {
            switch(source)
            {
                case LiteralInline literal:
                    target.Add(new Run(literal.Content.ToString()));
                    return;

                case CodeInline code:
                    var codeRun = new Run(code.Content)
                    {
                        FontFamily = new WpfFontFamily("Cascadia Mono, Consolas"),
                        FontSize = 13
                    };
                    codeRun.SetResourceReference(
                        TextElement.BackgroundProperty,
                        "CurrentControlBackground");
                    target.Add(codeRun);
                    return;

                case LineBreakInline lineBreak:
                    target.Add(lineBreak.IsHard
                        ? new LineBreak()
                        : new Run(" "));
                    return;

                case HtmlEntityInline entity:
                    target.Add(new Run(entity.Transcoded.ToString()));
                    return;

                case AutolinkInline autoLink:
                    AppendAutoLink(target, autoLink);
                    return;

                case EmphasisInline emphasis:
                    var span = new Span();
                    ApplyEmphasis(span, emphasis);
                    AppendChildren(span.Inlines, emphasis, context);
                    target.Add(span);
                    return;

                case LinkInline link when link.IsImage:
                    AppendImage(target, link, context);
                    return;

                case LinkInline link:
                    AppendLink(target, link, context);
                    return;

                case HtmlInline html:
                    target.Add(new Run(html.Tag));
                    return;

                case ContainerInline container:
                    AppendChildren(target, container, context);
                    return;
            }
        }

        private static void ApplyEmphasis(
            Span span,
            EmphasisInline emphasis)
        {
            if(emphasis.DelimiterChar == '~' &&
               emphasis.DelimiterCount >= 2)
            {
                span.TextDecorations = TextDecorations.Strikethrough;
                return;
            }

            if(emphasis.DelimiterCount >= 2)
                span.FontWeight = FontWeights.Bold;
            if(emphasis.DelimiterCount % 2 != 0)
                span.FontStyle = FontStyles.Italic;
        }

        private static void AppendLink(
            InlineCollection target,
            LinkInline source,
            RenderContext context)
        {
            string url = source.Url ?? string.Empty;
            if(!TryCreateSafeExternalUri(url, out Uri uri))
            {
                AppendChildren(target, source, context);
                return;
            }

            var hyperlink = new Hyperlink
            {
                NavigateUri = uri,
                ToolTip = uri.AbsoluteUri
            };
            hyperlink.SetResourceReference(
                TextElement.ForegroundProperty,
                "CurrentAccent");
            AppendChildren(hyperlink.Inlines, source, context);
            if(hyperlink.Inlines.Count == 0)
                hyperlink.Inlines.Add(new Run(url));
            target.Add(hyperlink);
        }

        private static void AppendAutoLink(
            InlineCollection target,
            AutolinkInline source)
        {
            string displayText = source.Url ?? string.Empty;
            string targetText = source.IsEmail
                ? $"mailto:{displayText}"
                : displayText;
            if(!TryCreateSafeExternalUri(targetText, out Uri uri))
            {
                target.Add(new Run(displayText));
                return;
            }

            var hyperlink = new Hyperlink(new Run(displayText))
            {
                NavigateUri = uri,
                ToolTip = uri.AbsoluteUri
            };
            hyperlink.SetResourceReference(
                TextElement.ForegroundProperty,
                "CurrentAccent");
            target.Add(hyperlink);
        }

        private static void AppendImage(
            InlineCollection target,
            LinkInline source,
            RenderContext context)
        {
            string alternateText = ExtractPlainText(source);
            WpfImage? image = TryLoadLocalImage(
                source.Url,
                alternateText,
                context.SourceDirectory);
            if(image is not null)
            {
                target.Add(new InlineUIContainer(image)
                {
                    BaselineAlignment = BaselineAlignment.Center
                });
                return;
            }

            var fallback = new Run(string.IsNullOrWhiteSpace(alternateText)
                ? "[image]"
                : alternateText)
            {
                FontStyle = FontStyles.Italic
            };
            fallback.SetResourceReference(
                TextElement.ForegroundProperty,
                "CurrentMutedForeground");
            target.Add(fallback);
        }

        private static void ApplyBlockSurfaceTheme(WpfBlock block)
        {
            block.SetResourceReference(
                TextElement.BackgroundProperty,
                "CurrentControlBackground");
            block.SetResourceReference(
                WpfBlock.BorderBrushProperty,
                "CurrentBorderColor");
        }

        private static WpfImage? TryLoadLocalImage(
            string? rawPath,
            string alternateText,
            string? sourceDirectory)
        {
            if(string.IsNullOrWhiteSpace(rawPath) ||
               string.IsNullOrWhiteSpace(sourceDirectory) ||
               Uri.TryCreate(rawPath, UriKind.Absolute, out _) ||
               Path.IsPathRooted(rawPath))
            {
                return null;
            }

            try
            {
                string pathPart = rawPath.Split(['?', '#'], 2)[0];
                string decodedPath = Uri.UnescapeDataString(pathPart)
                    .Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.GetFullPath(Path.Combine(
                    sourceDirectory,
                    decodedPath));
                var info = new FileInfo(fullPath);
                if(!info.Exists ||
                   info.Length <= 0 ||
                   info.Length > MaximumLocalImageBytes)
                {
                    return null;
                }

                using FileStream stream = new(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 1600;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return new WpfImage
                {
                    Source = bitmap,
                    MaxWidth = 900,
                    MaxHeight = 700,
                    Stretch = Stretch.Uniform,
                    ToolTip = string.IsNullOrWhiteSpace(alternateText)
                        ? Path.GetFileName(fullPath)
                        : alternateText
                };
            }
            catch(Exception exception) when(
                exception is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                FormatException or
                NotSupportedException or
                InvalidOperationException)
            {
                return null;
            }
        }

        private static string ExtractPlainText(ContainerInline source)
        {
            var text = new System.Text.StringBuilder();
            foreach(MdInline inline in source)
            {
                switch(inline)
                {
                    case LiteralInline literal:
                        text.Append(literal.Content.ToString());
                        break;
                    case CodeInline code:
                        text.Append(code.Content);
                        break;
                    case ContainerInline container:
                        text.Append(ExtractPlainText(container));
                        break;
                }
            }
            return text.ToString();
        }

        private static bool TryCreateSafeExternalUri(
            string url,
            out Uri uri)
        {
            uri = null!;
            if(!Uri.TryCreate(url, UriKind.Absolute, out Uri? candidate))
                return false;
            if(candidate.Scheme != Uri.UriSchemeHttp &&
               candidate.Scheme != Uri.UriSchemeHttps &&
               candidate.Scheme != Uri.UriSchemeMailto)
            {
                return false;
            }

            uri = candidate;
            return true;
        }

        private static string? GetSourceDirectory(string? markdownFilePath)
        {
            if(string.IsNullOrWhiteSpace(markdownFilePath))
                return null;
            try
            {
                return Path.GetDirectoryName(Path.GetFullPath(
                    markdownFilePath));
            }
            catch(Exception exception) when(
                exception is ArgumentException or NotSupportedException)
            {
                return null;
            }
        }

        private sealed record RenderContext(string? SourceDirectory);
    }
}

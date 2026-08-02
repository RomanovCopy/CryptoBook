using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Windows;
using System.Windows.Documents;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Services
{
    /// <summary>
    /// Переключает встроенные изображения между строчным и плавающим размещением,
    /// сохраняя корректную позицию каретки и структуру FlowDocument.
    /// </summary>
    public sealed class EmbeddedImageLayoutService:
        IEmbeddedImageLayoutService
    {
        private const string CaretAnchorText = "\u200B";
        private const string MoveAnchorText = "\u2060";

        private readonly IDocumentSession? documentSession;
        private readonly IInlineService? inlineService;

        public EmbeddedImageLayoutService(
            IDocumentSession? documentSession = null,
            IInlineService? inlineService = null)
        {
            this.documentSession = documentSession;
            this.inlineService = inlineService;
        }

        public ImageLayoutMode GetLayout(WpfImage image)
        {
            ArgumentNullException.ThrowIfNull(image);

            if(image.Parent is InlineUIContainer)
                return ImageLayoutMode.Inline;

            Figure figure = GetFigure(image)
                ?? throw new InvalidOperationException(
                    "Изображение находится в неподдерживаемом контейнере.");

            if(figure.WrapDirection == WrapDirection.None)
                return ImageLayoutMode.CenteredBlock;

            return figure.HorizontalAnchor switch
            {
                FigureHorizontalAnchor.ContentRight or
                FigureHorizontalAnchor.ColumnRight or
                FigureHorizontalAnchor.PageRight =>
                    ImageLayoutMode.FloatRight,
                _ => ImageLayoutMode.FloatLeft
            };
        }

        public void SetLayout(WpfImage image, ImageLayoutMode mode)
        {
            ArgumentNullException.ThrowIfNull(image);

            if(mode == ImageLayoutMode.Inline)
            {
                ConvertToInline(image);
                ConfigureInlineCaretAnchors(image);
                documentSession?.MarkDirty();
                return;
            }

            Figure figure = GetFigure(image)
                ?? ConvertToFigure(image);
            ConfigureFigure(figure, image, mode);
            ConfigureCaretAnchors(figure, mode);
            documentSession?.MarkDirty();
        }

        public TextPointer GetTextInsertionPosition(
            WpfImage image,
            ImageLayoutMode mode)
        {
            ArgumentNullException.ThrowIfNull(image);

            if(mode == ImageLayoutMode.Inline &&
               image.Parent is InlineUIContainer inlineContainer)
            {
                return inlineContainer.NextInline?.ContentStart
                    ?? inlineContainer.ElementEnd;
            }

            Figure figure = GetFigure(image)
                ?? throw new InvalidOperationException(
                    "Изображение не находится в ожидаемом контейнере.");

            if(mode == ImageLayoutMode.FloatRight)
            {
                return figure.PreviousInline?.ContentEnd
                    ?? figure.ElementStart;
            }

            return figure.NextInline?.ContentStart
                ?? figure.ElementEnd;
        }

        public bool Move(WpfImage image, TextPointer destination)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(destination);

            if(inlineService is null)
            {
                throw new InvalidOperationException(
                    "Сервис вставки элементов документа недоступен.");
            }

            Paragraph? destinationParagraph = destination.Paragraph;
            if(destinationParagraph is null ||
               IsInsideImage(destination, image))
            {
                return false;
            }

            ImageLayoutMode mode = GetLayout(image);
            // Временный невидимый Run материализует устойчивую позицию назначения:
            // TextPointer может измениться после извлечения изображения из дерева.
            var moveAnchor = new Run(MoveAnchorText);
            inlineService.InsertInlineAt(destination, moveAnchor);

            if(moveAnchor.Parent is not Paragraph paragraph)
            {
                if(moveAnchor.Parent is Span span)
                    span.Inlines.Remove(moveAnchor);
                return false;
            }

            DetachForMove(image);

            var container = new InlineUIContainer(image)
            {
                BaselineAlignment = BaselineAlignment.Center
            };
            paragraph.Inlines.InsertBefore(moveAnchor, container);
            paragraph.Inlines.Remove(moveAnchor);

            SetLayout(image, mode);
            return true;
        }

        public bool Remove(WpfImage image)
        {
            ArgumentNullException.ThrowIfNull(image);

            if(image.Parent is InlineUIContainer inlineContainer &&
               inlineContainer.Parent is Paragraph inlineParagraph)
            {
                inlineParagraph.Inlines.Remove(inlineContainer);
                documentSession?.MarkDirty();
                return true;
            }

            Figure? figure = GetFigure(image);
            if(figure?.Parent is Paragraph figureParagraph)
            {
                RemoveCaretAnchor(
                    figureParagraph,
                    figure.PreviousInline);
                RemoveCaretAnchor(
                    figureParagraph,
                    figure.NextInline);
                figureParagraph.Inlines.Remove(figure);
                documentSession?.MarkDirty();
                return true;
            }

            if(image.Parent is BlockUIContainer blockContainer &&
               blockContainer.Parent is FlowDocument document)
            {
                document.Blocks.Remove(blockContainer);
                documentSession?.MarkDirty();
                return true;
            }

            if(image.Parent is BlockUIContainer sectionBlock &&
               sectionBlock.Parent is Section section)
            {
                section.Blocks.Remove(sectionBlock);
                documentSession?.MarkDirty();
                return true;
            }

            return false;
        }

        private static Figure ConvertToFigure(WpfImage image)
        {
            if(image.Parent is not InlineUIContainer container ||
               container.Parent is not Paragraph paragraph)
            {
                throw new InvalidOperationException(
                    "Для изменения размещения требуется изображение в абзаце.");
            }

            Inline? previous = container.PreviousInline;
            Inline? next = container.NextInline;
            container.Child = null;
            paragraph.Inlines.Remove(container);

            var imageBlock = new BlockUIContainer(image)
            {
                Margin = new Thickness(0)
            };
            var figure = new Figure(imageBlock)
            {
                Padding = new Thickness(0),
                CanDelayPlacement = false,
                VerticalAnchor = FigureVerticalAnchor.ParagraphTop
            };

            InsertAtOriginalPosition(paragraph, figure, previous, next);
            return figure;
        }

        private static void ConvertToInline(WpfImage image)
        {
            if(image.Parent is InlineUIContainer)
                return;

            Figure figure = GetFigure(image)
                ?? throw new InvalidOperationException(
                    "Для строчного режима требуется изображение в Figure.");
            if(figure.Parent is not Paragraph paragraph ||
               image.Parent is not BlockUIContainer blockContainer)
            {
                throw new InvalidOperationException(
                    "Структура контейнера изображения повреждена.");
            }

            Inline? previous = figure.PreviousInline;
            Inline? next = figure.NextInline;
            RemoveCaretAnchor(paragraph, previous);
            RemoveCaretAnchor(paragraph, next);
            previous = figure.PreviousInline;
            next = figure.NextInline;
            blockContainer.Child = null;
            paragraph.Inlines.Remove(figure);

            var inlineContainer = new InlineUIContainer(image)
            {
                BaselineAlignment = BaselineAlignment.Center
            };
            InsertAtOriginalPosition(
                paragraph,
                inlineContainer,
                previous,
                next);
        }

        private static void ConfigureFigure(
            Figure figure,
            WpfImage image,
            ImageLayoutMode mode)
        {
            figure.HorizontalOffset = 0;
            figure.VerticalOffset = 0;
            figure.Width = new FigureLength(
                GetImageWidth(image),
                FigureUnitType.Pixel);

            switch(mode)
            {
                case ImageLayoutMode.CenteredBlock:
                    figure.HorizontalAnchor =
                        FigureHorizontalAnchor.ContentCenter;
                    figure.WrapDirection = WrapDirection.None;
                    figure.Margin = new Thickness(0, 6, 0, 6);
                    break;

                case ImageLayoutMode.FloatLeft:
                    figure.HorizontalAnchor =
                        FigureHorizontalAnchor.ContentLeft;
                    figure.WrapDirection = WrapDirection.Right;
                    figure.Margin = new Thickness(0, 4, 12, 6);
                    break;

                case ImageLayoutMode.FloatRight:
                    figure.HorizontalAnchor =
                        FigureHorizontalAnchor.ContentRight;
                    figure.WrapDirection = WrapDirection.Left;
                    figure.Margin = new Thickness(12, 4, 0, 6);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private static double GetImageWidth(WpfImage image)
        {
            if(double.IsFinite(image.Width) && image.Width > 0)
                return image.Width;
            if(image.ActualWidth > 0)
                return image.ActualWidth;
            if(image.Source?.Width > 0)
                return image.Source.Width;

            throw new InvalidOperationException(
                "Невозможно определить ширину изображения.");
        }

        private static void ConfigureCaretAnchors(
            Figure figure,
            ImageLayoutMode mode)
        {
            if(figure.Parent is not Paragraph paragraph)
                return;

            RemoveCaretAnchor(paragraph, figure.PreviousInline);
            RemoveCaretAnchor(paragraph, figure.NextInline);

            // Нулевые пробелы по сторонам Figure создают доступные каретке позиции;
            // без них пользователь не всегда может поставить курсор рядом с рисунком.
            if(mode is ImageLayoutMode.FloatRight or
               ImageLayoutMode.CenteredBlock &&
               figure.PreviousInline is null)
            {
                paragraph.Inlines.InsertBefore(
                    figure,
                    new Run(CaretAnchorText));
            }

            if(mode is ImageLayoutMode.FloatLeft or
               ImageLayoutMode.CenteredBlock &&
               figure.NextInline is null)
            {
                paragraph.Inlines.InsertAfter(
                    figure,
                    new Run(CaretAnchorText));
            }
        }

        private static void ConfigureInlineCaretAnchors(WpfImage image)
        {
            if(image.Parent is not InlineUIContainer container ||
               container.Parent is not Paragraph paragraph)
            {
                return;
            }

            RemoveCaretAnchor(paragraph, container.PreviousInline);
            RemoveCaretAnchor(paragraph, container.NextInline);

            if(container.PreviousInline is null)
                paragraph.Inlines.InsertBefore(
                    container,
                    new Run(CaretAnchorText));

            if(container.NextInline is null)
                paragraph.Inlines.InsertAfter(
                    container,
                    new Run(CaretAnchorText));
        }

        private static void RemoveCaretAnchor(
            Paragraph paragraph,
            Inline? inline)
        {
            if(inline is Run { Text: CaretAnchorText } marker)
                paragraph.Inlines.Remove(marker);
        }

        private static Figure? GetFigure(WpfImage image) =>
            image.Parent is BlockUIContainer blockContainer
                ? blockContainer.Parent as Figure
                : null;

        private static bool IsInsideImage(
            TextPointer position,
            WpfImage image)
        {
            TextElement? container = image.Parent switch
            {
                InlineUIContainer inlineContainer => inlineContainer,
                BlockUIContainer { Parent: Figure figure } => figure,
                _ => null
            };

            return container is not null &&
                   position.CompareTo(container.ElementStart) >= 0 &&
                   position.CompareTo(container.ElementEnd) <= 0;
        }

        private static void DetachForMove(WpfImage image)
        {
            if(image.Parent is InlineUIContainer inlineContainer &&
               inlineContainer.Parent is Paragraph inlineParagraph)
            {
                RemoveCaretAnchor(
                    inlineParagraph,
                    inlineContainer.PreviousInline);
                RemoveCaretAnchor(
                    inlineParagraph,
                    inlineContainer.NextInline);
                inlineContainer.Child = null;
                inlineParagraph.Inlines.Remove(inlineContainer);
                return;
            }

            Figure figure = GetFigure(image)
                ?? throw new InvalidOperationException(
                    "Изображение не находится в поддерживаемом контейнере.");
            if(figure.Parent is not Paragraph figureParagraph ||
               image.Parent is not BlockUIContainer blockContainer)
            {
                throw new InvalidOperationException(
                    "Структура контейнера изображения повреждена.");
            }

            RemoveCaretAnchor(
                figureParagraph,
                figure.PreviousInline);
            RemoveCaretAnchor(
                figureParagraph,
                figure.NextInline);
            blockContainer.Child = null;
            figureParagraph.Inlines.Remove(figure);
        }

        private static void InsertAtOriginalPosition(
            Paragraph paragraph,
            Inline inline,
            Inline? previous,
            Inline? next)
        {
            if(next?.Parent == paragraph)
            {
                paragraph.Inlines.InsertBefore(next, inline);
                return;
            }

            if(previous?.Parent == paragraph)
            {
                paragraph.Inlines.InsertAfter(previous, inline);
                return;
            }

            paragraph.Inlines.Add(inline);
        }
    }
}

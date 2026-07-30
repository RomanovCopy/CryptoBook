using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Windows;
using System.Windows.Documents;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.Services
{
    public sealed class EmbeddedImageLayoutService:
        IEmbeddedImageLayoutService
    {
        private readonly IDocumentSession? documentSession;

        public EmbeddedImageLayoutService(
            IDocumentSession? documentSession = null)
        {
            this.documentSession = documentSession;
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
                documentSession?.MarkDirty();
                return;
            }

            Figure figure = GetFigure(image)
                ?? ConvertToFigure(image);
            ConfigureFigure(figure, mode);
            documentSession?.MarkDirty();
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
            ImageLayoutMode mode)
        {
            figure.HorizontalOffset = 0;
            figure.VerticalOffset = 0;

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

        private static Figure? GetFigure(WpfImage image) =>
            image.Parent is BlockUIContainer blockContainer
                ? blockContainer.Parent as Figure
                : null;

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

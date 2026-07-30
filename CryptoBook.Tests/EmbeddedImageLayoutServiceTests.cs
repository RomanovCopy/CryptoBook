using CryptoBook.DTO;
using CryptoBook.Services;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class EmbeddedImageLayoutServiceTests
    {
        [WpfFact]
        public void SetLayout_ChangesAllModesWithoutMovingNeighboringText()
        {
            var service = new EmbeddedImageLayoutService();
            var image = new Image();
            var before = new Run("до");
            var after = new Run("после");
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(before);
            paragraph.Inlines.Add(new InlineUIContainer(image));
            paragraph.Inlines.Add(after);
            _ = new FlowDocument(paragraph);

            service.SetLayout(image, ImageLayoutMode.FloatLeft);

            var imageBlock =
                Assert.IsType<BlockUIContainer>(image.Parent);
            Figure figure = Assert.IsType<Figure>(imageBlock.Parent);
            Assert.Same(before, figure.PreviousInline);
            Assert.Same(after, figure.NextInline);
            Assert.Equal(
                FigureHorizontalAnchor.ContentLeft,
                figure.HorizontalAnchor);
            Assert.Equal(WrapDirection.Right, figure.WrapDirection);
            Assert.Equal(
                ImageLayoutMode.FloatLeft,
                service.GetLayout(image));

            service.SetLayout(image, ImageLayoutMode.FloatRight);

            Assert.Same(figure, imageBlock.Parent);
            Assert.Equal(
                FigureHorizontalAnchor.ContentRight,
                figure.HorizontalAnchor);
            Assert.Equal(WrapDirection.Left, figure.WrapDirection);
            Assert.Equal(
                ImageLayoutMode.FloatRight,
                service.GetLayout(image));

            service.SetLayout(image, ImageLayoutMode.CenteredBlock);

            Assert.Same(figure, imageBlock.Parent);
            Assert.Equal(
                FigureHorizontalAnchor.ContentCenter,
                figure.HorizontalAnchor);
            Assert.Equal(WrapDirection.None, figure.WrapDirection);
            Assert.Equal(
                ImageLayoutMode.CenteredBlock,
                service.GetLayout(image));

            service.SetLayout(image, ImageLayoutMode.Inline);

            var inline = Assert.IsType<InlineUIContainer>(image.Parent);
            Assert.Same(before, inline.PreviousInline);
            Assert.Same(after, inline.NextInline);
            Assert.Equal(
                ImageLayoutMode.Inline,
                service.GetLayout(image));
        }

        [WpfFact]
        public void Remove_RemovesInlineAndFloatingImages()
        {
            var service = new EmbeddedImageLayoutService();
            var inlineImage = new Image();
            var floatingImage = new Image();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new InlineUIContainer(inlineImage));
            paragraph.Inlines.Add(new Run("текст"));
            paragraph.Inlines.Add(new InlineUIContainer(floatingImage));
            _ = new FlowDocument(paragraph);
            service.SetLayout(
                floatingImage,
                ImageLayoutMode.FloatRight);

            Assert.True(service.Remove(inlineImage));
            var detachedInline =
                Assert.IsType<InlineUIContainer>(inlineImage.Parent);
            Assert.Null(detachedInline.Parent);
            Assert.True(service.Remove(floatingImage));
            var detachedBlock =
                Assert.IsType<BlockUIContainer>(floatingImage.Parent);
            var detachedFigure =
                Assert.IsType<Figure>(detachedBlock.Parent);
            Assert.Null(detachedFigure.Parent);
            Assert.Single(paragraph.Inlines);
        }
    }
}

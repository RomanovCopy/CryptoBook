using CryptoBook.Accessors;
using CryptoBook.DTO;
using CryptoBook.Interfaces;
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
            var image = new Image
            {
                Width = 120,
                Height = 80
            };
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
            Assert.Equal(120, figure.Width.Value);
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
            var floatingImage = new Image
            {
                Width = 120,
                Height = 80
            };
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

        [WpfTheory]
        [InlineData(ImageLayoutMode.FloatLeft)]
        [InlineData(ImageLayoutMode.FloatRight)]
        public void FloatingLayout_PlacesTextBesideImage(
            ImageLayoutMode mode)
        {
            var service = new EmbeddedImageLayoutService();
            var image = new Image
            {
                Width = 120,
                Height = 120,
                Source = new System.Windows.Media.Imaging.WriteableBitmap(
                    1,
                    1,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null)
            };
            var text = new Run(
                "Текст рядом с изображением должен занимать свободную сторону.");
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new InlineUIContainer(image));
            paragraph.Inlines.Add(text);
            var document = new FlowDocument(paragraph)
            {
                PagePadding = new Thickness(0),
                ColumnWidth = double.PositiveInfinity
            };
            var editor = new RichTextBox
            {
                Width = 500,
                Height = 300,
                Document = document
            };

            service.SetLayout(image, mode);
            editor.Measure(new Size(500, 300));
            editor.Arrange(new Rect(0, 0, 500, 300));
            editor.UpdateLayout();

            Rect imageBounds = image.TransformToAncestor(editor)
                .TransformBounds(new Rect(image.RenderSize));
            Rect textBounds = text.ContentStart.GetCharacterRect(
                LogicalDirection.Forward);

            Assert.True(imageBounds.Width > 0);
            Assert.True(textBounds.Top < imageBounds.Bottom);
            if(mode == ImageLayoutMode.FloatLeft)
                Assert.True(textBounds.Left >= imageBounds.Right);
            else
                Assert.True(textBounds.Left <= imageBounds.Left);
        }

        [WpfFact]
        public void FloatLeft_CreatesCaretTargetToTheRightOfImage()
        {
            var service = new EmbeddedImageLayoutService();
            var image = new Image
            {
                Width = 120,
                Height = 120,
                Source = new System.Windows.Media.Imaging.WriteableBitmap(
                    1,
                    1,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null)
            };
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new InlineUIContainer(image));
            var document = new FlowDocument(paragraph)
            {
                PagePadding = new Thickness(0),
                ColumnWidth = double.PositiveInfinity
            };
            var editor = new RichTextBox
            {
                Width = 500,
                Height = 300,
                Document = document
            };

            service.SetLayout(image, ImageLayoutMode.FloatLeft);
            editor.Measure(new Size(500, 300));
            editor.Arrange(new Rect(0, 0, 500, 300));
            editor.UpdateLayout();

            Rect imageBounds = image.TransformToAncestor(editor)
                .TransformBounds(new Rect(image.RenderSize));
            TextPointer position =
                service.GetTextInsertionPosition(
                    image,
                    ImageLayoutMode.FloatLeft);

            Rect caretBounds = position.GetCharacterRect(
                LogicalDirection.Forward);
            Assert.True(caretBounds.Top < imageBounds.Bottom);
            Assert.True(caretBounds.Left >= imageBounds.Right);
            Assert.InRange(
                caretBounds.Top,
                imageBounds.Top - 8,
                imageBounds.Top + 30);
        }

        [WpfTheory]
        [InlineData(ImageLayoutMode.Inline, true, true)]
        [InlineData(ImageLayoutMode.CenteredBlock, true, true)]
        [InlineData(ImageLayoutMode.FloatLeft, false, true)]
        [InlineData(ImageLayoutMode.FloatRight, true, false)]
        public void IsolatedImage_CreatesCaretTargetsForLayout(
            ImageLayoutMode mode,
            bool expectsBefore,
            bool expectsAfter)
        {
            var service = new EmbeddedImageLayoutService();
            var image = new Image
            {
                Width = 120,
                Height = 80
            };
            var container = new InlineUIContainer(image);
            var paragraph = new Paragraph(container);
            _ = new FlowDocument(paragraph);

            service.SetLayout(image, mode);

            Inline imageInline = mode == ImageLayoutMode.Inline
                ? Assert.IsType<InlineUIContainer>(image.Parent)
                : Assert.IsType<Figure>(
                    Assert.IsType<BlockUIContainer>(image.Parent).Parent);
            Assert.Equal(
                expectsBefore,
                imageInline.PreviousInline is Run { Text: "\u200B" });
            Assert.Equal(
                expectsAfter,
                imageInline.NextInline is Run { Text: "\u200B" });
        }

        [WpfTheory]
        [InlineData(ImageLayoutMode.Inline)]
        [InlineData(ImageLayoutMode.CenteredBlock)]
        [InlineData(ImageLayoutMode.FloatLeft)]
        [InlineData(ImageLayoutMode.FloatRight)]
        public void Move_PreservesLayoutAndInsertsAtTextPosition(
            ImageLayoutMode mode)
        {
            var paragraphFactory = new TestParagraphFactory();
            IRichTextBoxService richTextBox = new RichTextBoxService(
                paragraphFactory,
                new TestUriNavigationService(),
                new DocumentAppearanceDefaults());
            var inlineService = new InlineService(
                richTextBox,
                new ReflectionPropertyAccessor(),
                paragraphFactory);
            var service = new EmbeddedImageLayoutService(
                inlineService: inlineService);
            var image = new Image
            {
                Width = 120,
                Height = 80
            };
            var sourceParagraph = new Paragraph();
            sourceParagraph.Inlines.Add(new InlineUIContainer(image));
            sourceParagraph.Inlines.Add(new Run("исходный текст"));
            var destinationRun = new Run("слева справа");
            var destinationParagraph = new Paragraph(destinationRun);
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(sourceParagraph);
            richTextBox.Document.Blocks.Add(destinationParagraph);
            service.SetLayout(image, mode);
            TextPointer destination =
                destinationRun.ContentStart.GetPositionAtOffset(6)!;

            bool moved = service.Move(image, destination);

            Assert.True(moved);
            Assert.Equal(mode, service.GetLayout(image));
            Assert.DoesNotContain(
                sourceParagraph.Inlines,
                inline => ContainsImage(inline, image));
            Assert.Contains(
                destinationParagraph.Inlines,
                inline => ContainsImage(inline, image));
            string destinationText = string.Concat(
                destinationParagraph.Inlines
                    .OfType<Run>()
                    .Select(run => run.Text));
            Assert.Equal(
                "слева справа",
                destinationText.Replace("\u200B", string.Empty));
        }

        [WpfFact]
        public void Move_WithinSameParagraph_KeepsAllText()
        {
            var paragraphFactory = new TestParagraphFactory();
            IRichTextBoxService richTextBox = new RichTextBoxService(
                paragraphFactory,
                new TestUriNavigationService(),
                new DocumentAppearanceDefaults());
            var inlineService = new InlineService(
                richTextBox,
                new ReflectionPropertyAccessor(),
                paragraphFactory);
            var service = new EmbeddedImageLayoutService(
                inlineService: inlineService);
            var image = new Image
            {
                Width = 120,
                Height = 80
            };
            var destinationRun = new Run("середина конец");
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("начало "));
            paragraph.Inlines.Add(new InlineUIContainer(image));
            paragraph.Inlines.Add(destinationRun);
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(paragraph);
            service.SetLayout(image, ImageLayoutMode.FloatLeft);
            TextPointer destination =
                destinationRun.ContentStart.GetPositionAtOffset(8)!;

            Assert.True(service.Move(image, destination));

            Assert.Equal(
                "начало середина конец",
                string.Concat(
                    paragraph.Inlines
                        .OfType<Run>()
                        .Select(run => run.Text))
                    .Replace("\u200B", string.Empty));
            Figure figure = Assert.IsType<Figure>(
                Assert.IsType<BlockUIContainer>(image.Parent).Parent);
            Assert.Equal("середина", Assert.IsType<Run>(
                figure.PreviousInline).Text);
        }

        [WpfTheory]
        [InlineData(ImageLayoutMode.Inline)]
        [InlineData(ImageLayoutMode.CenteredBlock)]
        [InlineData(ImageLayoutMode.FloatLeft)]
        [InlineData(ImageLayoutMode.FloatRight)]
        public void MoveUpAndDown_MovesBetweenAdjacentParagraphs(
            ImageLayoutMode mode)
        {
            var paragraphFactory = new TestParagraphFactory();
            IRichTextBoxService richTextBox = new RichTextBoxService(
                paragraphFactory,
                new TestUriNavigationService(),
                new DocumentAppearanceDefaults());
            var inlineService = new InlineService(
                richTextBox,
                new ReflectionPropertyAccessor(),
                paragraphFactory);
            var service = new EmbeddedImageLayoutService(
                inlineService: inlineService);
            var image = new Image
            {
                Width = 120,
                Height = 80
            };
            var first = new Paragraph(new Run("первый"));
            var second = new Paragraph();
            second.Inlines.Add(new InlineUIContainer(image));
            second.Inlines.Add(new Run("второй"));
            var third = new Paragraph(new Run("третий"));
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(first);
            richTextBox.Document.Blocks.Add(second);
            richTextBox.Document.Blocks.Add(third);
            service.SetLayout(image, mode);

            Assert.True(service.CanMoveUp(image));
            Assert.True(service.CanMoveDown(image));

            Assert.True(service.MoveUp(image));
            Assert.Contains(
                first.Inlines,
                inline => ContainsImage(inline, image));
            Assert.Equal(mode, service.GetLayout(image));
            Assert.False(service.CanMoveUp(image));

            Assert.True(service.MoveDown(image));
            Assert.Contains(
                second.Inlines,
                inline => ContainsImage(inline, image));
            Assert.True(service.MoveDown(image));
            Assert.Contains(
                third.Inlines,
                inline => ContainsImage(inline, image));
            Assert.Equal(mode, service.GetLayout(image));
            Assert.False(service.CanMoveDown(image));
        }

        [WpfFact]
        public void MoveUpAndDown_SingleParagraph_DoNothing()
        {
            var paragraphFactory = new TestParagraphFactory();
            IRichTextBoxService richTextBox = new RichTextBoxService(
                paragraphFactory,
                new TestUriNavigationService(),
                new DocumentAppearanceDefaults());
            var inlineService = new InlineService(
                richTextBox,
                new ReflectionPropertyAccessor(),
                paragraphFactory);
            var service = new EmbeddedImageLayoutService(
                inlineService: inlineService);
            var image = new Image
            {
                Width = 120,
                Height = 80
            };
            var paragraph = new Paragraph(
                new InlineUIContainer(image));
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(paragraph);

            Assert.False(service.CanMoveUp(image));
            Assert.False(service.CanMoveDown(image));
            Assert.False(service.MoveUp(image));
            Assert.False(service.MoveDown(image));
            Assert.Contains(
                paragraph.Inlines,
                inline => ContainsImage(inline, image));
        }

        private static bool ContainsImage(
            Inline inline,
            Image image) =>
            inline switch
            {
                InlineUIContainer container =>
                    ReferenceEquals(container.Child, image),
                Figure
                {
                    Blocks.FirstBlock:
                        BlockUIContainer blockContainer
                } => ReferenceEquals(blockContainer.Child, image),
                _ => false
            };

        private sealed class TestParagraphFactory: IParagraphFactory
        {
            public IParagraphService Create(Inline? inline = null)
            {
                var paragraph = new ParagraphService();
                if(inline is not null)
                    paragraph.Inlines.Add(inline);
                return paragraph;
            }
        }
    }
}

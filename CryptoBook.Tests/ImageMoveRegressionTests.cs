using CryptoBook.Accessors;
using CryptoBook.Behaviors;
using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ImageMoveRegressionTests
{
    [WpfTheory]
    [InlineData(ImageLayoutMode.Inline, 0)]
    [InlineData(ImageLayoutMode.Inline, 3)]
    [InlineData(ImageLayoutMode.Inline, 6)]
    [InlineData(ImageLayoutMode.CenteredBlock, 0)]
    [InlineData(ImageLayoutMode.CenteredBlock, 3)]
    [InlineData(ImageLayoutMode.CenteredBlock, 6)]
    [InlineData(ImageLayoutMode.FloatLeft, 0)]
    [InlineData(ImageLayoutMode.FloatLeft, 3)]
    [InlineData(ImageLayoutMode.FloatLeft, 6)]
    [InlineData(ImageLayoutMode.FloatRight, 0)]
    [InlineData(ImageLayoutMode.FloatRight, 3)]
    [InlineData(ImageLayoutMode.FloatRight, 6)]
    public void Move_IntoNestedFormattedText_PreservesTextAndExactPosition(
        ImageLayoutMode mode, int offset)
    {
        var (editor, service) = CreateEditor();
        var image = new Image { Width = 120, Height = 80 };
        var source = new Paragraph(new InlineUIContainer(image));
        var run = new Run("abcdef");
        var link = new Hyperlink(new Bold(run))
        {
            NavigateUri = new Uri("https://example.com/image-test")
        };
        var destination = new Paragraph(link);
        editor.Document.Blocks.Add(source);
        editor.Document.Blocks.Add(destination);
        service.SetLayout(image, mode);

        Assert.True(service.Move(image, run.ContentStart.GetPositionAtOffset(offset)!));

        Inline imageInline = GetImageInline(image);
        Assert.Same(destination, imageInline.Parent);
        Assert.Equal(mode, service.GetLayout(image));
        Assert.Equal("abcdef", VisibleText(destination.ContentStart, destination.ContentEnd));
        Assert.Equal("abcdef"[..offset], VisibleText(destination.ContentStart, imageInline.ElementStart));
        Assert.Equal("abcdef"[offset..], VisibleText(imageInline.ElementEnd, destination.ContentEnd));
        foreach(Run text in Descendants(destination.Inlines).OfType<Run>().Where(text =>
            text.Text.Any(character => character is not '\u200B' and not '\u2060')))
        {
            Assert.Equal(FontWeights.Bold, text.FontWeight);
            Assert.Equal(link.NavigateUri, FindHyperlink(text)?.NavigateUri);
        }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void MoveDown_ToParagraphStart_InsertsBeforeText(bool formatted)
    {
        var (editor, service) = CreateEditor();
        var image = new Image { Width = 120, Height = 80 };
        var source = new Paragraph(new InlineUIContainer(image));
        var run = new Run("destination");
        var destination = new Paragraph(formatted ? new Bold(run) : run);
        editor.Document.Blocks.Add(source);
        editor.Document.Blocks.Add(destination);

        Assert.True(service.MoveDown(image));

        Inline imageInline = GetImageInline(image);
        Assert.Equal(string.Empty, VisibleText(destination.ContentStart, imageInline.ElementStart));
        Assert.Equal("destination", VisibleText(imageInline.ElementEnd, destination.ContentEnd));
    }

    [WpfTheory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void InsertInlineAt_InsideNestedRun_KeepsOwningSpanAndText(int offset)
    {
        var factory = new TestParagraphFactory();
        var editor = new RichTextBoxService(factory, new TestUriNavigationService(), new DocumentAppearanceDefaults());
        var service = new InlineService(editor, new ReflectionPropertyAccessor(), factory);
        var run = new Run("abcdef");
        var bold = new Bold(run);
        var paragraph = new Paragraph(bold);
        editor.Document.Blocks.Clear();
        editor.Document.Blocks.Add(paragraph);
        var marker = new Run("|");

        service.InsertInlineAt(run.ContentStart.GetPositionAtOffset(offset)!, marker);

        Assert.Same(bold, marker.Parent);
        Assert.Equal("abcdef".Insert(offset, "|"), VisibleText(paragraph.ContentStart, paragraph.ContentEnd));
    }

    [WpfTheory]
    [InlineData(ImageLayoutMode.Inline)]
    [InlineData(ImageLayoutMode.CenteredBlock)]
    [InlineData(ImageLayoutMode.FloatLeft)]
    [InlineData(ImageLayoutMode.FloatRight)]
    public void ImageInsertedInsideFormatting_CanChangeLayoutMoveAndRemove(ImageLayoutMode mode)
    {
        var factory = new TestParagraphFactory();
        var (editor, layout) = CreateEditor();
        var inlines = new InlineService(editor, new ReflectionPropertyAccessor(), factory);
        var run = new Run("abcdef");
        var paragraph = new Paragraph(new Bold(run));
        var nextParagraph = new Paragraph(new Run("next"));
        editor.Document.Blocks.Add(paragraph);
        editor.Document.Blocks.Add(nextParagraph);
        var image = new Image { Width = 120, Height = 80 };
        inlines.InsertInlineAt(run.ContentStart.GetPositionAtOffset(3)!, new InlineUIContainer(image));

        layout.SetLayout(image, mode);

        Assert.Same(paragraph, GetImageInline(image).Parent);
        Assert.Equal(mode, layout.GetLayout(image));
        Assert.Equal("abc", VisibleText(paragraph.ContentStart, GetImageInline(image).ElementStart));
        Assert.True(layout.MoveDown(image));
        Assert.True(layout.Remove(image));
        Assert.Equal("abcdef", VisibleText(paragraph.ContentStart, paragraph.ContentEnd));
        Assert.Equal("next", VisibleText(nextParagraph.ContentStart, nextParagraph.ContentEnd));
        Assert.All(Descendants(paragraph.Inlines).OfType<Run>(), text => Assert.Equal(FontWeights.Bold, text.FontWeight));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedImage_FromLoadedDocument_CanMoveOrRemoveWithoutChangingLayoutFirst(bool remove)
    {
        var (editor, layout) = CreateEditor();
        var image = new Image { Width = 120, Height = 80 };
        var bold = new Bold(new Run("before"));
        bold.Inlines.Add(new InlineUIContainer(image));
        bold.Inlines.Add(new Run("after"));
        var first = new Paragraph(bold);
        var second = new Paragraph(new Run("next"));
        editor.Document.Blocks.Add(first);
        editor.Document.Blocks.Add(second);

        Assert.True(layout.CanMoveDown(image));
        Assert.True(remove ? layout.Remove(image) : layout.MoveDown(image));

        Assert.Equal("beforeafter", VisibleText(first.ContentStart, first.ContentEnd));
        if(!remove)
            Assert.Same(second, GetImageInline(image).Parent);
    }

    [WpfTheory]
    [InlineData(ImageLayoutMode.Inline)]
    [InlineData(ImageLayoutMode.CenteredBlock)]
    [InlineData(ImageLayoutMode.FloatLeft)]
    [InlineData(ImageLayoutMode.FloatRight)]
    public async Task MoveCommands_RepeatedAtFullScreenSize_KeepImageAndEditorUsable(ImageLayoutMode mode)
    {
        var (service, layout) = CreateEditor();
        RichTextBox editor = service;
        var bitmap = BitmapSource.Create(640, 480, 96, 96, PixelFormats.Bgr32, null,
            new byte[640 * 480 * 4], 640 * 4);
        bitmap.Freeze();
        var image = new Image { Source = bitmap, Width = 640, Height = 480 };
        var first = new Paragraph(new InlineUIContainer(image));
        var second = new Paragraph(new Bold(new Run("formatted destination")));
        service.Document.Blocks.Add(first);
        service.Document.Blocks.Add(second);
        layout.SetLayout(image, mode);
        var host = new AdornerDecorator { Child = editor };
        var window = new Window
        {
            Content = host, Width = 1920, Height = 1080,
            Left = -10000, Top = -10000, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false
        };
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        DocumentImageResizeBehavior.SetImageEditor(editor, new EmbeddedImageEditor());
        DocumentImageResizeBehavior.SetImageLayoutService(editor, layout);
        DocumentImageResizeBehavior.SetIsEnabled(editor, true);
        try
        {
            window.Show();
            Layout(1920, 1080);
            for(int iteration = 0; iteration < 20; iteration++)
            {
                SelectImage();
                Assert.True(DocumentImageResizeBehavior.MoveImageDownCommand.CanExecute(null, editor));
                DocumentImageResizeBehavior.MoveImageDownCommand.Execute(null, editor);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Layout(iteration % 2 == 0 ? 2560 : 1920, 1440);
                Assert.Same(second, GetImageInline(image).Parent);
                Assert.Equal(string.Empty, VisibleText(second.ContentStart, GetImageInline(image).ElementStart));

                Assert.True(DocumentImageResizeBehavior.MoveImageUpCommand.CanExecute(null, editor));
                DocumentImageResizeBehavior.MoveImageUpCommand.Execute(null, editor);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Layout(1920, 1080);
                Assert.Same(first, GetImageInline(image).Parent);
                Assert.Equal(mode, layout.GetLayout(image));
                Assert.Equal("formatted destination", VisibleText(second.ContentStart, second.ContentEnd));
            }

            var rendered = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(host);
            Assert.True(image.IsArrangeValid);
            Assert.Equal(2, service.Document.Blocks.Count);
            Assert.DoesNotContain('\u2060', new TextRange(service.Document.ContentStart, service.Document.ContentEnd).Text);
        }
        finally
        {
            DocumentImageResizeBehavior.SetIsEnabled(editor, false);
            DocumentPageBorderBehavior.SetIsEnabled(editor, false);
            window.Close();
        }

        void SelectImage() => editor.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
        {
            RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent,
            Source = image
        });

        void Layout(double width, double height)
        {
            // Размер тестового viewport не зависит от разрешения монитора,
            // ограничивающего максимальный размер скрытого HWND.
            host.Width = width;
            host.Height = height;
            window.UpdateLayout();
            Assert.Equal(width, host.ActualWidth, 3);
            Assert.Equal(height, host.ActualHeight, 3);
        }
    }

    [WpfFact]
    public void Move_UndoAndRedo_PreserveImageAndFormattedText()
    {
        var (service, layout) = CreateEditor();
        var image = new Image { Width = 120, Height = 80 };
        var first = new Paragraph(new InlineUIContainer(image));
        var second = new Paragraph(new Bold(new Run("destination")));
        service.Document.Blocks.Add(first);
        service.Document.Blocks.Add(second);
        var window = new Window
        {
            Content = service, Width = 960, Height = 600,
            Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false
        };
        window.Show();
        try
        {
            service.IsUndoEnabled = false;
            service.IsUndoEnabled = true;

            service.BeginChange();
            try
            {
                Assert.True(layout.MoveDown(image));
            }
            finally
            {
                service.EndChange();
            }

            Assert.True(service.CanUndo);
            service.Undo();
            var restoredFirst = Assert.IsType<Paragraph>(service.Document.Blocks.FirstBlock);
            var restoredSecond = Assert.IsType<Paragraph>(service.Document.Blocks.LastBlock);
            Image restoredImage = Assert.IsType<Image>(Assert.Single(
                Descendants(restoredFirst.Inlines).OfType<InlineUIContainer>()).Child);
            Assert.Equal(120, restoredImage.Width);
            Assert.Equal("destination", VisibleText(restoredSecond.ContentStart, restoredSecond.ContentEnd));
            Assert.True(service.CanRedo);
            service.Redo();
            restoredSecond = Assert.IsType<Paragraph>(service.Document.Blocks.LastBlock);
            InlineUIContainer restoredContainer = Assert.Single(
                Descendants(restoredSecond.Inlines).OfType<InlineUIContainer>());
            Assert.IsType<Image>(restoredContainer.Child);
            Assert.Equal(string.Empty, VisibleText(restoredSecond.ContentStart, restoredContainer.ElementStart));
        }
        finally
        {
            window.Close();
        }
    }

    private static (RichTextBoxService Editor, EmbeddedImageLayoutService Service) CreateEditor()
    {
        var factory = new TestParagraphFactory();
        var editor = new RichTextBoxService(factory, new TestUriNavigationService(), new DocumentAppearanceDefaults());
        editor.Document.Blocks.Clear();
        return (editor, new EmbeddedImageLayoutService(inlineService:
            new InlineService(editor, new ReflectionPropertyAccessor(), factory)));
    }

    private static Inline GetImageInline(Image image) => image.Parent switch
    {
        InlineUIContainer inline => inline,
        BlockUIContainer block => Assert.IsType<Figure>(block.Parent),
        _ => throw new InvalidOperationException("Image lost its document container.")
    };

    private static string VisibleText(TextPointer start, TextPointer end)
    {
        var text = new System.Text.StringBuilder();
        for(TextPointer? position = start; position is not null && position.CompareTo(end) < 0;
            position = position.GetNextContextPosition(LogicalDirection.Forward))
        {
            if(position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                string run = position.GetTextInRun(LogicalDirection.Forward);
                text.Append(run.AsSpan(0, Math.Min(run.Length, position.GetOffsetToPosition(end))));
            }
        }
        return text.ToString().Replace("\u200B", string.Empty).Replace("\u2060", string.Empty);
    }

    private static IEnumerable<Inline> Descendants(InlineCollection inlines)
    {
        foreach(Inline inline in inlines)
        {
            yield return inline;
            if(inline is Span span)
                foreach(Inline child in Descendants(span.Inlines))
                    yield return child;
        }
    }

    private static Hyperlink? FindHyperlink(FrameworkContentElement element) => element.Parent switch
    {
        Hyperlink link => link,
        FrameworkContentElement parent => FindHyperlink(parent),
        _ => null
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

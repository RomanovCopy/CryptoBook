using CryptoBook.Accessors;
using CryptoBook.Behaviors;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Markup;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace CryptoBook.Tests;

public sealed class FontTypingTests
{
    [WpfFact]
    public void ColorPalette_ContainsEverySupportedColorInDisplayOrder()
    {
        var (_, fonts) = CreateServices(new Run("text"));
        DrawingColor[] expected =
        [
            DrawingColor.Black,
            DrawingColor.DimGray,
            DrawingColor.Gray,
            DrawingColor.DarkGray,
            DrawingColor.Silver,
            DrawingColor.LightGray,
            DrawingColor.Gainsboro,
            DrawingColor.White,
            DrawingColor.Maroon,
            DrawingColor.DarkRed,
            DrawingColor.Red,
            DrawingColor.Crimson,
            DrawingColor.IndianRed,
            DrawingColor.Salmon,
            DrawingColor.LightCoral,
            DrawingColor.OrangeRed,
            DrawingColor.Orange,
            DrawingColor.Gold,
            DrawingColor.Yellow,
            DrawingColor.Olive,
            DrawingColor.DarkGreen,
            DrawingColor.Green,
            DrawingColor.SeaGreen,
            DrawingColor.LimeGreen,
            DrawingColor.YellowGreen,
            DrawingColor.Lime,
            DrawingColor.Teal,
            DrawingColor.DarkCyan,
            DrawingColor.Cyan,
            DrawingColor.Turquoise,
            DrawingColor.LightBlue,
            DrawingColor.SteelBlue,
            DrawingColor.Blue,
            DrawingColor.Navy,
            DrawingColor.Indigo,
            DrawingColor.Purple,
            DrawingColor.Magenta,
            DrawingColor.DeepPink,
            DrawingColor.Pink,
            DrawingColor.Brown,
            DrawingColor.SaddleBrown,
            DrawingColor.Chocolate,
            DrawingColor.Tan,
            DrawingColor.Beige,
            DrawingColor.Transparent
        ];

        Assert.Equal(expected, fonts.FontColors);
        Assert.Equal(
            fonts.FontColors.Count,
            fonts.FontColors.Select(color => color.ToArgb()).Distinct().Count());
    }

    [WpfFact]
    public void DefaultPaperAndFontColors_DoNotFollowThemeResources()
    {
        var (service, _) = CreateServices(new Run("text"));

        service.Service.Resources["CurrentDocumentBackground"] = Brushes.Red;
        service.Service.Resources["CurrentWindowForeground"] = Brushes.Blue;

        Assert.Equal(
            Colors.White,
            Assert.IsType<SolidColorBrush>(service.Document.Background).Color);
        Assert.Equal(
            Colors.Black,
            Assert.IsType<SolidColorBrush>(service.Document.Foreground).Color);
        Assert.Equal(
            Colors.White,
            Assert.IsType<SolidColorBrush>(service.BackGround).Color);
        Assert.Equal(
            Colors.Black,
            Assert.IsType<SolidColorBrush>(service.Service.Foreground).Color);
    }

    [WpfFact]
    public void HighContrastTextCursor_InstanceIsReusable()
    {
        Assert.NotNull(HighContrastTextCursor.Instance);
        Assert.Same(HighContrastTextCursor.Instance, HighContrastTextCursor.Instance);
    }

    [WpfFact]
    public void ImageResizeBehavior_DoesNotClearCursorWhenEditorIsUnloaded()
    {
        var (service, _) = CreateServices(new Run("text"));
        service.Service.Cursor = HighContrastTextCursor.Instance;
        DocumentImageResizeBehavior.SetIsEnabled(service.Service, true);

        service.Service.RaiseEvent(
            new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        Assert.Same(HighContrastTextCursor.Instance, service.Service.Cursor);
        DocumentImageResizeBehavior.SetIsEnabled(service.Service, false);
    }

    [WpfFact]
    public void FontWeight_IsAppliedToTextTypedBetweenAdjacentCharacters()
    {
        var (service, fonts) = CreateServices(new Run("ab"));
        var original = GetOnlyRun(service);
        service.CaretPosition = original.ContentStart.GetPositionAtOffset(1)!;

        fonts.SetFontWeight(FontWeights.Bold);
        service.InsertTextAtCaret("X");

        Assert.Equal("aXb", GetText(service));
        AssertCharacterProperty(service, 0, TextElement.FontWeightProperty, FontWeights.Normal);
        AssertCharacterProperty(service, 1, TextElement.FontWeightProperty, FontWeights.Bold);
        AssertCharacterProperty(service, 2, TextElement.FontWeightProperty, FontWeights.Normal);
    }

    [WpfFact]
    public void EveryFontProperty_TypesForwardFromEmptyDocument()
    {
        foreach(var (name, apply) in FontPropertyChanges())
        {
            var (service, fonts) = CreateServices();
            var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;

            apply(fonts);
            RaisePreviewTextInput(service, "A");

            Assert.Equal("A", GetText(service));
            Assert.Equal(
                1,
                GetTextOffset(paragraph, service.CaretPosition));

            RaisePreviewTextInput(service, "B");

            Assert.Equal("AB", GetText(service));
            Assert.Equal(
                2,
                GetTextOffset(paragraph, service.CaretPosition));
        }
    }

    [WpfFact]
    public void EveryFontProperty_DoesNotRestoreStaleEmptyCaretAfterFirstCharacter()
    {
        foreach(var (name, apply) in FontPropertyChanges())
        {
            var (service, fonts) = CreateServices();
            var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
            service.Service.RaiseEvent(
                new RoutedEventArgs(UIElement.LostFocusEvent));

            apply(fonts);
            RaisePreviewTextInput(service, "A");
            service.RestoreSelection();

            Assert.True(
                service.Selection.IsEmpty,
                $"{name} unexpectedly restored a selection.");
            Assert.Equal(
                1,
                GetTextOffset(paragraph, service.CaretPosition));

            RaisePreviewTextInput(service, "B");

            Assert.Equal("AB", GetText(service));
            Assert.Equal(
                2,
                GetTextOffset(paragraph, service.CaretPosition));
        }
    }

    [WpfFact]
    public void EveryFontProperty_TypesForwardFromInitialEditorDocument()
    {
        foreach(var (name, apply) in FontPropertyChanges())
        {
            var (service, fonts) = CreateInitialServices();
            var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;

            apply(fonts);
            service.InsertTextAtCaret("A");

            Assert.Equal(
                1,
                GetTextOffset(paragraph, service.CaretPosition));

            service.InsertTextAtCaret("B");

            Assert.StartsWith("AB", new TextRange(
                paragraph.ContentStart,
                paragraph.ContentEnd).Text);
            Assert.Equal(
                2,
                GetTextOffset(paragraph, service.CaretPosition));
        }
    }

    [WpfFact]
    public void CombinedFontProperties_AreAppliedOnlyToNewAdjacentCharacter()
    {
        var (service, fonts) = CreateServices(
            new Run("a") { Foreground = System.Windows.Media.Brushes.Black },
            new Run("b") { Foreground = System.Windows.Media.Brushes.Blue });
        var first = ((Paragraph)service.Document.Blocks.FirstBlock!).Inlines.FirstInline!;
        service.CaretPosition = first.ContentEnd;

        fonts.SetFontWeight(FontWeights.Bold);
        fonts.SetFontStyle(FontStyles.Italic);
        fonts.SetFontStretch(FontStretches.Expanded);
        fonts.SetFontColor(DrawingColor.Red);
        fonts.SetFontBackground(DrawingColor.Yellow);
        fonts.SetFontFamily(new System.Windows.Media.FontFamily("Consolas"));
        fonts.SetFontSize(24);
        fonts.SetTextDecoration(TextDecorations.Underline);
        service.InsertTextAtCaret("X");

        Assert.Equal("aXb", GetText(service));
        AssertCharacterProperty(service, 1, TextElement.FontWeightProperty, FontWeights.Bold);
        AssertCharacterProperty(service, 1, TextElement.FontStyleProperty, FontStyles.Italic);
        AssertCharacterProperty(service, 1, TextElement.FontStretchProperty, FontStretches.Expanded);
        AssertCharacterBrush(service, 1, TextElement.ForegroundProperty, Colors.Red);
        AssertCharacterBrush(service, 1, TextElement.BackgroundProperty, Colors.Yellow);
        AssertCharacterProperty(service, 1, TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas"));
        AssertCharacterProperty(service, 1, TextElement.FontSizeProperty, 24d);
        Assert.Contains(
            TextDecorations.Underline[0],
            Assert.IsType<TextDecorationCollection>(
                GetCharacterRange(service, 1).GetPropertyValue(Inline.TextDecorationsProperty)));
        AssertCharacterBrush(service, 0, TextElement.ForegroundProperty, Colors.Black);
        AssertCharacterBrush(service, 2, TextElement.ForegroundProperty, Colors.Blue);
    }

    [WpfFact]
    public void FontColor_UpdatesCaretBrushWithActiveSelection()
    {
        var (service, fonts) = CreateServices(new Run("text"));
        var run = GetOnlyRun(service);
        service.Selection.Select(run.ContentStart, run.ContentEnd);

        fonts.SetFontColor(DrawingColor.Red);

        Assert.Equal(
            Colors.Red,
            Assert.IsType<SolidColorBrush>(service.CaretBrush).Color);
    }

    [WpfFact]
    public void MovingCaret_UsesForegroundAtCurrentTextPosition()
    {
        var (service, _) = CreateServices(
            new Run("red") { Foreground = Brushes.Red },
            new Run("blue") { Foreground = Brushes.Blue });
        var runs = ((Paragraph)service.Document.Blocks.FirstBlock!)
            .Inlines
            .OfType<Run>()
            .ToArray();

        TextPointer redPosition = runs[0].ContentStart.GetPositionAtOffset(1)!;
        service.Selection.Select(redPosition, redPosition);

        Assert.Equal(
            Colors.Red,
            Assert.IsType<SolidColorBrush>(service.CaretBrush).Color);

        TextPointer bluePosition = runs[1].ContentStart.GetPositionAtOffset(1)!;
        service.Selection.Select(bluePosition, bluePosition);

        Assert.Equal(
            Colors.Blue,
            Assert.IsType<SolidColorBrush>(service.CaretBrush).Color);
    }

    [WpfFact]
    public void PendingFontColor_RemainsCaretColorAfterTyping()
    {
        var (service, fonts) = CreateServices(new Run("ab"));
        var run = GetOnlyRun(service);
        service.CaretPosition = run.ContentStart.GetPositionAtOffset(1)!;
        service.ClearSelection();

        fonts.SetFontColor(DrawingColor.Red);
        service.InsertTextAtCaret("X");

        Assert.Equal(
            Colors.Red,
            Assert.IsType<SolidColorBrush>(service.CaretBrush).Color);
        Assert.Equal(2, GetTextOffset(
            (Paragraph)service.Document.Blocks.FirstBlock!,
            service.CaretPosition));
    }

    [WpfFact]
    public void TypingProperties_StayActiveForFollowingCharacters()
    {
        var (service, fonts) = CreateServices(new Run("ab"));
        var original = GetOnlyRun(service);
        service.CaretPosition = original.ContentStart.GetPositionAtOffset(1)!;

        fonts.SetFontWeight(FontWeights.Bold);
        fonts.SetFontStyle(FontStyles.Italic);
        service.InsertTextAtCaret("X");
        service.InsertTextAtCaret("Y");

        Assert.Equal("aXYb", GetText(service));
        AssertCharacterProperty(service, 1, TextElement.FontWeightProperty, FontWeights.Bold);
        AssertCharacterProperty(service, 2, TextElement.FontWeightProperty, FontWeights.Bold);
        AssertCharacterProperty(service, 1, TextElement.FontStyleProperty, FontStyles.Italic);
        AssertCharacterProperty(service, 2, TextElement.FontStyleProperty, FontStyles.Italic);
        AssertCharacterProperty(service, 0, TextElement.FontWeightProperty, FontWeights.Normal);
        AssertCharacterProperty(service, 3, TextElement.FontWeightProperty, FontWeights.Normal);
    }

    [WpfFact]
    public void MovingCaret_DoesNotLeakPendingPropertiesToAnotherPosition()
    {
        var (service, fonts) = CreateServices(new Run("ab"));
        var original = GetOnlyRun(service);
        service.CaretPosition = original.ContentStart.GetPositionAtOffset(1)!;

        fonts.SetFontWeight(FontWeights.Bold);
        service.CaretPosition = original.ContentEnd;
        service.ClearSelection();
        service.InsertTextAtCaret("X");

        Assert.Equal("abX", GetText(service));
        AssertCharacterProperty(service, 2, TextElement.FontWeightProperty, FontWeights.Normal);
    }

    [WpfFact]
    public void FormattingOneOfTwoAdjacentCharacters_DoesNotChangeItsNeighbour()
    {
        var (service, fonts) = CreateServices(new Run("ab"));
        var run = GetOnlyRun(service);
        service.Selection.Select(
            run.ContentStart.GetPositionAtOffset(1)!,
            run.ContentStart.GetPositionAtOffset(2)!);

        fonts.SetFontWeight(FontWeights.Bold);
        fonts.SetFontStyle(FontStyles.Italic);
        fonts.SetFontColor(DrawingColor.Red);

        Assert.Equal("ab", GetText(service));
        AssertCharacterProperty(service, 0, TextElement.FontWeightProperty, FontWeights.Normal);
        AssertCharacterProperty(service, 0, TextElement.FontStyleProperty, FontStyles.Normal);
        AssertCharacterBrush(service, 0, TextElement.ForegroundProperty, Colors.Black);
        AssertCharacterProperty(service, 1, TextElement.FontWeightProperty, FontWeights.Bold);
        AssertCharacterProperty(service, 1, TextElement.FontStyleProperty, FontStyles.Italic);
        AssertCharacterBrush(service, 1, TextElement.ForegroundProperty, Colors.Red);
    }

    [WpfFact]
    public void EveryFontProperty_PreservesCollapsedCaretPosition()
    {
        foreach(var (name, apply) in FontPropertyChanges())
        {
            var (service, fonts) = CreateServices(new Run("abcd"));
            var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
            var caret = paragraph.ContentStart.GetPositionAtOffset(2)!;
            service.CaretPosition = caret;
            service.ClearSelection();
            var caretOffset = GetTextOffset(paragraph, caret);

            apply(fonts);

            Assert.True(
                service.Selection.IsEmpty,
                $"{name} unexpectedly created a selection.");
            Assert.Equal(caretOffset, GetTextOffset(paragraph, service.CaretPosition));
            Assert.Equal(caretOffset, GetTextOffset(paragraph, service.Selection.Start));
            Assert.Equal(caretOffset, GetTextOffset(paragraph, service.Selection.End));
        }
    }

    [WpfFact]
    public void EveryFontProperty_PreservesActiveSelectionAndCaret()
    {
        foreach(var (name, apply) in FontPropertyChanges())
        {
            var (service, fonts) = CreateServices(new Run("abcd"));
            var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
            var start = paragraph.ContentStart.GetPositionAtOffset(1)!;
            var end = paragraph.ContentStart.GetPositionAtOffset(3)!;
            service.Selection.Select(start, end);
            var startOffset = GetTextOffset(paragraph, service.Selection.Start);
            var endOffset = GetTextOffset(paragraph, service.Selection.End);
            var caretOffset = GetTextOffset(paragraph, service.CaretPosition);

            apply(fonts);

            Assert.False(
                service.Selection.IsEmpty,
                $"{name} unexpectedly collapsed the selection.");
            Assert.Equal(startOffset, GetTextOffset(paragraph, service.Selection.Start));
            Assert.Equal(endOffset, GetTextOffset(paragraph, service.Selection.End));
            Assert.Equal(caretOffset, GetTextOffset(paragraph, service.CaretPosition));
        }
    }

    [WpfFact]
    public void EveryToolbarFontProperty_RestoresAndPreservesSelectionAfterLostFocus()
    {
        foreach(var (name, apply) in FontPropertyChanges())
        {
            var (service, fonts) = CreateServices(new Run("abcd"));
            var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
            var start = paragraph.ContentStart.GetPositionAtOffset(1)!;
            var end = paragraph.ContentStart.GetPositionAtOffset(3)!;
            service.Selection.Select(start, end);
            service.Service.RaiseEvent(
                new RoutedEventArgs(UIElement.LostFocusEvent));
            service.CaretPosition = paragraph.ContentEnd;
            service.Selection.Select(paragraph.ContentEnd, paragraph.ContentEnd);
            var startOffset = GetTextOffset(paragraph, start);
            var endOffset = GetTextOffset(paragraph, end);

            apply(fonts);

            Assert.False(
                service.Selection.IsEmpty,
                $"{name} did not restore the selection.");
            Assert.Equal(startOffset, GetTextOffset(paragraph, service.Selection.Start));
            Assert.Equal(endOffset, GetTextOffset(paragraph, service.Selection.End));
            Assert.Equal(endOffset, GetTextOffset(paragraph, service.CaretPosition));
        }
    }

    [WpfFact]
    public void FormattingAfterClickInsidePreviousSelection_DoesNotRestoreStaleSelection()
    {
        var (service, fonts) = CreateServices(new Run("abcd"));
        var editor = Assert.IsType<RichTextBoxService>(service.Service);
        var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
        var start = paragraph.ContentStart.GetPositionAtOffset(1)!;
        var clickedPosition = paragraph.ContentStart.GetPositionAtOffset(2)!;
        var end = paragraph.ContentStart.GetPositionAtOffset(3)!;
        var clickedTextOffset = GetTextOffset(paragraph, clickedPosition);
        service.Selection.Select(start, end);
        service.Service.RaiseEvent(
            new RoutedEventArgs(UIElement.LostFocusEvent));

        // PreviewMouseLeftButtonDown runs before WPF collapses the selection
        // to the clicked caret position.
        editor.DismissSelectionIfOutside(clickedPosition);
        service.CaretPosition = clickedPosition;
        service.Selection.Select(clickedPosition, clickedPosition);

        fonts.SetFontStyle(FontStyles.Italic);

        Assert.True(service.Selection.IsEmpty);
        Assert.Equal(
            clickedTextOffset,
            GetTextOffset(paragraph, service.CaretPosition));
    }

    [WpfFact]
    public void ToolbarFormatting_RestoresSelectionCapturedWhenEditorLostFocus()
    {
        var (service, fonts) = CreateServices(new Run("ab"));
        var run = GetOnlyRun(service);
        var start = run.ContentStart;
        var end = start.GetPositionAtOffset(1)!;
        service.Selection.Select(start, end);

        service.Service.RaiseEvent(
            new RoutedEventArgs(UIElement.LostFocusEvent));
        service.CaretPosition = run.ContentEnd;
        service.Selection.Select(run.ContentEnd, run.ContentEnd);

        fonts.SetFontWeight(FontWeights.Bold);

        Assert.True(service.Service.IsInactiveSelectionHighlightEnabled);
        Assert.False(service.Selection.IsEmpty);
        AssertCharacterProperty(
            service,
            0,
            TextElement.FontWeightProperty,
            FontWeights.Bold);
        AssertCharacterProperty(
            service,
            1,
            TextElement.FontWeightProperty,
            FontWeights.Normal);
    }

    [WpfFact]
    public void EveryFontProperty_IsAppliedExactlyToMixedSelection()
    {
        var (service, fonts) = CreateServices(
            new Run("a") { FontWeight = FontWeights.Bold, Foreground = Brushes.Blue },
            new Run("b") { FontWeight = FontWeights.Light, Foreground = Brushes.Green });
        var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
        service.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);

        fonts.SetFontWeight(FontWeights.SemiBold);
        fonts.SetFontStyle(FontStyles.Italic);
        fonts.SetFontStretch(FontStretches.Expanded);
        fonts.SetFontFamily(new FontFamily("Arial"));
        fonts.SetFontSize(22);
        fonts.SetFontColor(DrawingColor.Red);
        fonts.SetFontBackground(DrawingColor.Yellow);
        fonts.SetTextDecoration(TextDecorations.Strikethrough);

        for(var offset = 0; offset < 2; offset++)
        {
            AssertCharacterProperty(service, offset, TextElement.FontWeightProperty, FontWeights.SemiBold);
            AssertCharacterProperty(service, offset, TextElement.FontStyleProperty, FontStyles.Italic);
            AssertCharacterProperty(service, offset, TextElement.FontStretchProperty, FontStretches.Expanded);
            AssertCharacterProperty(service, offset, TextElement.FontFamilyProperty, new FontFamily("Arial"));
            AssertCharacterProperty(service, offset, TextElement.FontSizeProperty, 22d);
            AssertCharacterBrush(service, offset, TextElement.ForegroundProperty, Colors.Red);
            AssertCharacterBrush(service, offset, TextElement.BackgroundProperty, Colors.Yellow);
            Assert.Contains(
                TextDecorations.Strikethrough[0],
                Assert.IsType<TextDecorationCollection>(
                    GetCharacterRange(service, offset).GetPropertyValue(Inline.TextDecorationsProperty)));
        }
    }

    [WpfFact]
    public void ClearFormatting_WithEmptySelection_AppliesDefaultsToFollowingText()
    {
        var (service, fonts) = CreateServices(new Run("ab"));
        var run = GetOnlyRun(service);
        service.CaretPosition = run.ContentStart.GetPositionAtOffset(1)!;

        fonts.SetFontWeight(FontWeights.Bold);
        fonts.SetFontColor(DrawingColor.Red);
        fonts.ClearFormatting();
        service.InsertTextAtCaret("X");

        Assert.Equal("aXb", GetText(service));
        AssertCharacterProperty(service, 1, TextElement.FontWeightProperty, fonts.DefaultFontWeight);
        AssertCharacterProperty(service, 1, TextElement.FontSizeProperty, fonts.DefaultFontSize);
        AssertCharacterBrush(service, 1, TextElement.ForegroundProperty, Colors.Black);
    }

    [WpfFact]
    public void GetFontSizeInSelection_ReturnsSizeOrZeroForMixedSelection()
    {
        var (service, _) = CreateServices(
            new Run("a") { FontSize = 18 },
            new Run("b") { FontSize = 18 });
        var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
        service.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);

        Assert.Equal(18, service.GetFontSizeInSelection());

        paragraph.Inlines.LastInline!.FontSize = 24;
        Assert.Equal(0, service.GetFontSizeInSelection());
    }

    [WpfFact]
    public void DocumentBackground_ColorsPaperWithoutChangingCharacterBackground()
    {
        var (service, fonts) = CreateServices(new Run("text"));
        var run = GetOnlyRun(service);
        service.Selection.Select(run.ContentStart, run.ContentEnd);

        fonts.SetDocumentBackground(DrawingColor.Yellow);

        var documentBrush =
            Assert.IsType<SolidColorBrush>(service.Document.Background);
        var editorBrush =
            Assert.IsType<SolidColorBrush>(service.BackGround);
        Assert.Equal(Colors.Yellow, documentBrush.Color);
        Assert.Equal(Colors.Yellow, editorBrush.Color);
        object characterBackground = new TextRange(
            run.ContentStart,
            run.ContentEnd)
            .GetPropertyValue(TextElement.BackgroundProperty);
        Assert.True(
            characterBackground is not SolidColorBrush brush ||
            brush.Color != Colors.Yellow);
    }

    [WpfFact]
    public void DocumentBackgroundImage_ReplacesPaperAndCanBeCleared()
    {
        var (service, fonts) = CreateServices(new Run("text"));
        BitmapSource bitmap = CreateBitmap();

        fonts.SetDocumentBackground(DrawingColor.Yellow);
        fonts.SetDocumentBackgroundImage(bitmap);

        var imageBrush = Assert.IsType<ImageBrush>(
            service.Document.Background);
        Assert.Same(bitmap, imageBrush.ImageSource);
        Assert.Same(imageBrush, service.BackGround);
        Assert.Equal(Stretch.UniformToFill, imageBrush.Stretch);
        Assert.True(fonts.HasDocumentBackgroundImage);

        fonts.ClearDocumentBackgroundImage();

        Assert.Equal(
            Colors.Yellow,
            Assert.IsType<SolidColorBrush>(
                service.Document.Background).Color);
        Assert.False(fonts.HasDocumentBackgroundImage);
    }

    [WpfFact]
    public async Task DocumentBackgroundImageCommand_LoadsAndAppliesSelectedImage()
    {
        IRichTextBoxService service = new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());
        var inline = new InlineService(
            service,
            new ReflectionPropertyAccessor(),
            new TestParagraphFactory());
        var fonts = new FontService(
            service,
            inline,
            new DocumentBackgroundPreferenceStoreStub(),
            new DocumentAppearanceDefaults());
        BitmapSource bitmap = CreateBitmap();
        var viewModel = new FontFormatBar_ViewModel(
            fonts,
            inline,
            service,
            new ImageFilePickerStub("background.png"),
            new ImageContentLoaderStub(bitmap));

        await Assert.IsAssignableFrom<IAsyncCommand>(
            viewModel.ChooseDocumentBackgroundImageCommand)
            .ExecuteAsync();

        var brush = Assert.IsType<ImageBrush>(service.Document.Background);
        Assert.Same(bitmap, brush.ImageSource);
        Assert.Same(brush, service.BackGround);
        Assert.Equal(
            fonts.DocumentBackground.ToArgb(),
            viewModel.DocumentBackground!.Value.ToArgb());
    }

    [WpfFact]
    public void DocumentBackgroundImage_IsRenderedByEditor()
    {
        var (service, fonts) = CreateServices(new Run("text"));
        fonts.SetDocumentBackgroundImage(CreateBitmap());
        var editor = service.Service;
        editor.Width = 240;
        editor.Height = 120;
        editor.Measure(new Size(editor.Width, editor.Height));
        editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));
        editor.UpdateLayout();

        var rendered = new RenderTargetBitmap(
            240,
            120,
            96,
            96,
            PixelFormats.Pbgra32);
        rendered.Render(editor);
        byte[] pixels = new byte[240 * 120 * 4];
        rendered.CopyPixels(pixels, 240 * 4, 0);

        Assert.Contains(
            Enumerable.Range(0, pixels.Length / 4),
            index =>
            {
                int offset = index * 4;
                byte blue = pixels[offset];
                byte green = pixels[offset + 1];
                byte red = pixels[offset + 2];
                byte alpha = pixels[offset + 3];
                return alpha > 200 &&
                    ((red > 200 && green < 80 && blue < 80) ||
                     (green > 200 && red < 80 && blue < 80));
            });
    }

    [WpfFact]
    public void DocumentBackgroundImage_KeepsDocumentCoordinatesAcrossResizeZoomAndScroll()
    {
        var (service, fonts) = CreateServices(new Run("CryptoBook — фон закреплён на странице"));
        var pixels = new byte[400 * 400 * 4];
        for(int y = 0; y < 400; y++)
            for(int x = 0; x < 400; x++)
            {
                int offset = (y * 400 + x) * 4;
                pixels[offset] = 40;
                pixels[offset + 1] = (byte)(y * 255 / 399);
                pixels[offset + 2] = (byte)(x * 255 / 399);
                pixels[offset + 3] = 255;
            }
        fonts.SetDocumentBackgroundImage(BitmapSource.Create(400, 400, 96, 96,
            PixelFormats.Bgra32, null, pixels, 1600));
        var editor = service.Service;
        editor.Padding = new Thickness(0);
        editor.BorderThickness = new Thickness(0);
        editor.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
        editor.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
        var host = new AdornerDecorator { Child = editor };
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        DocumentBackgroundImageBehavior.SetFontService(editor, fonts);
        void Layout(int width, int height)
        {
            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();
        }
        byte[] Sample(string name = "baseline")
        {
            host.UpdateLayout();
            int width = (int)host.ActualWidth, height = (int)host.ActualHeight;
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            if(Environment.GetEnvironmentVariable("CRYPTOBOOK_UI_QA_DIR") is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
                using var stream = File.Create(Path.Combine(directory, "anchored-" + name + ".png"));
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
            var data = new byte[width * height * 4];
            bitmap.CopyPixels(data, width * 4, 0);
            var point = editor.TranslatePoint(new Point(250 - editor.HorizontalOffset, 250 - editor.VerticalOffset), host);
            int offset = ((int)Math.Round(point.Y) * width + (int)Math.Round(point.X)) * 4;
            return data[offset..(offset + 4)];
        }
        void SamePixel(byte[] expected, string operation)
        {
            var actual = Sample(operation);
            for(int i = 0; i < 4; i++)
                Assert.True(Math.Abs(expected[i] - actual[i]) <= 2,
                    $"{operation}: document point changed from {string.Join(',', expected)} to {string.Join(',', actual)}");
        }
        Layout(900, 500);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        host.UpdateLayout();
        var baseline = Sample();
        var background = service.Document.Background;
        Layout(900, 750);
        SamePixel(baseline, "Resize height");
        Layout(1100, 750);
        SamePixel(baseline, "Resize width");
        DocumentPageBorderBehavior.HandleMouseWheel(editor, -480, System.Windows.Input.ModifierKeys.Control);
        SamePixel(baseline, "Zoom out");
        Assert.Same(background, service.Document.Background);
        for(int i = 0; i < 80; i++)
            service.Document.Blocks.Add(new Paragraph(new Run("Строка " + i)) { LineHeight = 30 });
        host.UpdateLayout();
        // Growing the continuous sheet changes its height; resizing its window must not.
        baseline = Sample("long-document");
        Layout(900, 500);
        SamePixel(baseline, "Resize long document");
        editor.ScrollToVerticalOffset(100);
        host.UpdateLayout();
        Assert.True(editor.VerticalOffset > 0);
        SamePixel(baseline, "Scroll vertically");
        DocumentPageBorderBehavior.HandleMouseWheel(editor, 1200, System.Windows.Input.ModifierKeys.Control);
        host.UpdateLayout();
        SamePixel(baseline, "Zoom in");
        editor.ScrollToHorizontalOffset(100);
        host.UpdateLayout();
        Assert.True(editor.HorizontalOffset > 0);
        SamePixel(baseline, "Scroll horizontally");
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    [WpfFact]
    public void DocumentBackgroundImage_DragRequiresControlPreservesSelectionAndCancelsWithEscape()
    {
        var (service, fonts) = CreateServices(new Run("Выделенный текст"));
        fonts.SetDocumentBackgroundImage(CreateBitmap());
        var editor = service.Service;
        DocumentBackgroundImageBehavior.SetFontService(editor, fonts);
        var window = new Window
        {
            Content = editor, Width = 900, Height = 600,
            Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            service.Selection.Select(service.Document.ContentStart, service.Document.ContentEnd);
            var selection = service.Selection.Text;
            var point = new Point(100, 100);
            Assert.False(DocumentBackgroundImageBehavior.BeginDrag(editor, point, System.Windows.Input.ModifierKeys.None));
            editor.IsReadOnly = true;
            Assert.False(DocumentBackgroundImageBehavior.BeginDrag(editor, point, System.Windows.Input.ModifierKeys.Control));
            editor.IsReadOnly = false;
            Assert.False(DocumentBackgroundImageBehavior.BeginDrag(editor, new Point(-20, 100), System.Windows.Input.ModifierKeys.Control));
            Assert.True(DocumentBackgroundImageBehavior.BeginDrag(editor, point, System.Windows.Input.ModifierKeys.Control));
            Assert.True(editor.IsMouseCaptured);
            Assert.True(DocumentBackgroundImageBehavior.MoveDrag(editor, new Point(180, 100),
                System.Windows.Input.ModifierKeys.Control, System.Windows.Input.MouseButtonState.Pressed));
            Assert.NotEqual(new Rect(0, 0, 1, 1), ((ImageBrush)service.Document.Background).Viewbox);
            Assert.Equal(selection, service.Selection.Text);
            Assert.True(DocumentBackgroundImageBehavior.MoveDrag(editor, point,
                System.Windows.Input.ModifierKeys.Control, System.Windows.Input.MouseButtonState.Pressed));
            Assert.Equal(new Rect(0, 0, 1, 1), ((ImageBrush)service.Document.Background).Viewbox);
            Assert.True(DocumentBackgroundImageBehavior.MoveDrag(editor, new Point(180, 100),
                System.Windows.Input.ModifierKeys.Control, System.Windows.Input.MouseButtonState.Pressed));
            editor.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(editor), 0, System.Windows.Input.Key.Escape)
            { RoutedEvent = UIElement.PreviewKeyDownEvent });
            Assert.False(editor.IsMouseCaptured);
            Assert.Equal(new Rect(0, 0, 1, 1), ((ImageBrush)service.Document.Background).Viewbox);
            Assert.Equal(selection, service.Selection.Text);
            Assert.True(DocumentBackgroundImageBehavior.BeginDrag(editor, point, System.Windows.Input.ModifierKeys.Control));
            editor.ReleaseMouseCapture();
            Assert.False(DocumentBackgroundImageBehavior.MoveDrag(editor, new Point(220, 100),
                System.Windows.Input.ModifierKeys.Control, System.Windows.Input.MouseButtonState.Pressed));
            var original = (ImageBrush)service.Document.Background;
            fonts.CommitDocumentBackgroundImage(original, DocumentBackgroundSaveOperation.Crop(original, service.Document.PageWidth));
            Assert.False(DocumentBackgroundImageBehavior.BeginDrag(editor, point, System.Windows.Input.ModifierKeys.Control));
            fonts.SetDocumentBackgroundImage(CreateBitmap());
            window.UpdateLayout();
            Assert.True(DocumentBackgroundImageBehavior.BeginDrag(editor, point, System.Windows.Input.ModifierKeys.Control));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void DocumentBackgroundImage_ClipsToPaperAndMovesVisibleCropAtReducedZoom()
    {
        var (service, fonts) = CreateServices(new Run("Фон страницы — Ctrl + левая кнопка мыши"));
        fonts.SetDocumentBackgroundImage(CreateBitmap());
        var editor = service.Service;
        editor.Padding = new Thickness(0);
        editor.BorderThickness = new Thickness(0);
        editor.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
        editor.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
        var host = new AdornerDecorator { Child = editor };
        DocumentPageBorderBehavior.SetIsEnabled(editor, true);
        DocumentBackgroundImageBehavior.SetFontService(editor, fonts);
        host.Measure(new Size(1000, 500));
        host.Arrange(new Rect(0, 0, 1000, 500));
        host.UpdateLayout();
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        host.UpdateLayout();
        DocumentPageBorderBehavior.HandleMouseWheel(editor, -1200, System.Windows.Input.ModifierKeys.Control);
        host.UpdateLayout();

        byte[] Render(string name)
        {
            host.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1000, 500, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            var pixels = new byte[1000 * 500 * 4];
            bitmap.CopyPixels(pixels, 4000, 0);
            if(Environment.GetEnvironmentVariable("CRYPTOBOOK_UI_QA_DIR") is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
                using var stream = File.Create(Path.Combine(directory, name + ".png"));
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
            return pixels;
        }
        var before = Render("background-before");
        Assert.Equal(0, before[(200 * 1000 + 700) * 4 + 3]);
        Assert.Equal(255, before[(200 * 1000 + 100) * 4 + 3]);
        var start = DocumentBackgroundImageLayout.GetVisibleCrop(
            (ImageBrush)service.Document.Background, new Size(service.Document.PageWidth, editor.ViewportHeight));
        var moved = DocumentBackgroundImageLayout.MoveCrop(start,
            new Size(service.Document.PageWidth, editor.ViewportHeight), new Vector(10000, 10000));
        Assert.Equal(0, moved.X);
        Assert.Equal(0, moved.Y);
        fonts.SetDocumentBackgroundImageCrop(moved);
        var after = Render("background-after");
        Assert.Equal(0, after[(200 * 1000 + 700) * 4 + 3]);
        Assert.False(before.SequenceEqual(after), "Moving the crop must visibly change the rendered paper.");

        for(int i = 0; i < 80; i++)
            service.Document.Blocks.Add(new Paragraph(new Run("Строка документа " + i)));
        host.UpdateLayout();
        editor.ScrollToVerticalOffset(200);
        host.UpdateLayout();
        var scrolled = Render("background-scrolled");
        Assert.True(editor.VerticalOffset > 0);
        Assert.Equal(0, scrolled[(200 * 1000 + 700) * 4 + 3]);
        Assert.Equal(255, scrolled[(200 * 1000 + 100) * 4 + 3]);
        var translucent = ((ImageBrush)service.Document.Background).CloneCurrentValue();
        translucent.Opacity = 0.4;
        service.Document.Background = translucent;
        service.BackGround = translucent;
        var alpha = Render("background-opacity");
        Assert.InRange(alpha[(210 * 1000 + 350) * 4 + 3], 101, 103);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Same(service.Document.Background, editor.Background);
    }

    [WpfTheory]
    [InlineData(200, 100, 100, 200, 0.25, 1)]
    [InlineData(100, 200, 200, 100, 1, 0.25)]
    public void DocumentBackgroundImage_CropPreservesAspectAndClampsBothDirections(
        int imageWidth, int imageHeight, double paperWidth, double paperHeight, double cropWidth, double cropHeight)
    {
        var bitmap = BitmapSource.Create(imageWidth, imageHeight, 96, 96, PixelFormats.Bgra32,
            null, new byte[imageWidth * imageHeight * 4], imageWidth * 4);
        var paper = new Size(paperWidth, paperHeight);
        var crop = DocumentBackgroundImageLayout.GetVisibleCrop(new ImageBrush(bitmap), paper);
        Assert.Equal(cropWidth, crop.Width, 6);
        Assert.Equal(cropHeight, crop.Height, 6);
        var end = DocumentBackgroundImageLayout.MoveCrop(crop, paper, new Vector(-10000, -10000));
        Assert.Equal(1, end.Right, 6);
        Assert.Equal(1, end.Bottom, 6);
        var start = DocumentBackgroundImageLayout.MoveCrop(crop, paper, new Vector(10000, 10000));
        Assert.Equal(0, start.X);
        Assert.Equal(0, start.Y);
    }

    [WpfFact]
    public void ReplaceDocument_PreservesLoadedDocumentBackground()
    {
        var (service, _) = CreateInitialServices();
        var loadedBackground = new ImageBrush(CreateBitmap())
        {
            Stretch = Stretch.UniformToFill
        };
        var loadedDocument = new FlowDocument(
            new Paragraph(new Run("loaded")))
        {
            Background = loadedBackground
        };

        service.ReplaceDocument(loadedDocument);

        Assert.Same(loadedBackground, service.Document.Background);
        Assert.Same(loadedBackground, service.BackGround);
    }

    [WpfFact]
    public void DocumentBackgroundChanges_MarkDocumentSessionDirty()
    {
        IRichTextBoxService service = new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());
        var session = new DocumentSession(service);
        var inline = new InlineService(
            service,
            new ReflectionPropertyAccessor(),
            new TestParagraphFactory(),
            session);
        var fonts = new FontService(
            service,
            inline,
            new DocumentBackgroundPreferenceStoreStub(),
            new DocumentAppearanceDefaults(),
            session);
        session.Open(
            Path.Combine(Path.GetTempPath(), "background.XamlPackage"),
            new XamlPackageFileTemplate());

        fonts.SetDocumentBackground(DrawingColor.Yellow);

        Assert.True(session.IsDirty);

        session.MarkSaved(
            session.FilePath!,
            session.Template!);
        fonts.SetDocumentBackgroundImage(CreateBitmap());

        Assert.True(session.IsDirty);

        session.MarkSaved(
            session.FilePath!,
            session.Template!);
        var selectionStart = service.Selection.Start;
        var selectionEnd = service.Selection.End;
        fonts.SetDocumentBackgroundImageCrop(new Rect(0.1, 0, 0.5, 1));
        Assert.True(session.IsDirty);
        Assert.Equal(0, selectionStart.CompareTo(service.Selection.Start));
        Assert.Equal(0, selectionEnd.CompareTo(service.Selection.End));
        session.MarkSaved(session.FilePath!, session.Template!);
        fonts.ClearDocumentBackgroundImage();

        Assert.True(session.IsDirty);
    }

    [WpfFact]
    public void ClearFormatting_ResetsEveryPropertyOnlyInsideSelection()
    {
        var formatted = new Run("ab")
        {
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontStretch = FontStretches.Expanded,
            FontFamily = new FontFamily("Arial"),
            FontSize = 28,
            Foreground = Brushes.Red,
            Background = Brushes.Yellow,
            TextDecorations = TextDecorations.Underline
        };
        var (service, fonts) = CreateServices(formatted);
        var run = GetOnlyRun(service);
        service.Selection.Select(run.ContentStart, run.ContentStart.GetPositionAtOffset(1)!);

        fonts.ClearFormatting();

        AssertCharacterProperty(service, 0, TextElement.FontWeightProperty, fonts.DefaultFontWeight);
        AssertCharacterProperty(service, 0, TextElement.FontStyleProperty, fonts.DefaultFontStyle);
        AssertCharacterProperty(service, 0, TextElement.FontStretchProperty, fonts.DefaultFontStretch);
        AssertCharacterProperty(service, 0, TextElement.FontFamilyProperty, fonts.DefaultFontFamily);
        AssertCharacterProperty(service, 0, TextElement.FontSizeProperty, fonts.DefaultFontSize);
        AssertCharacterBrush(service, 0, TextElement.ForegroundProperty, Colors.Black);
        AssertCharacterProperty(service, 1, TextElement.FontWeightProperty, FontWeights.Bold);
        AssertCharacterBrush(service, 1, TextElement.ForegroundProperty, Colors.Red);
    }

    [WpfFact]
    public void OrdinaryTextDocument_CharacterFormattingIgnoresSelection()
    {
        IRichTextBoxService service = new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());
        var session = new DocumentSession(service);
        session.Open(
            Path.Combine(Path.GetTempPath(), "document.md"),
            new PlainTextTemplate());
        var inline = new InlineService(
            service,
            new ReflectionPropertyAccessor(),
            new TestParagraphFactory(),
            session);
        var fonts = new FontService(
            service,
            inline,
            new DocumentBackgroundPreferenceStoreStub(),
            new DocumentAppearanceDefaults(),
            session);
        var paragraph = new Paragraph(new Run("ab"));
        service.Document.Blocks.Clear();
        service.Document.Blocks.Add(paragraph);
        service.Selection.Select(
            paragraph.ContentStart,
            paragraph.ContentStart.GetPositionAtOffset(1)!);

        fonts.SetFontWeight(FontWeights.Bold);

        AssertCharacterProperty(
            service,
            0,
            TextElement.FontWeightProperty,
            FontWeights.Bold);
        AssertCharacterProperty(
            service,
            1,
            TextElement.FontWeightProperty,
            FontWeights.Bold);
    }

    private static (IRichTextBoxService Service, FontService Fonts) CreateServices(params Run[] runs)
    {
        IRichTextBoxService service = new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());
        var paragraph = new Paragraph();
        foreach(var run in runs)
            paragraph.Inlines.Add(run);

        service.Document.Blocks.Clear();
        service.Document.Blocks.Add(paragraph);
        service.CaretPosition = paragraph.ContentStart;

        var inline = new InlineService(service, new ReflectionPropertyAccessor(), new TestParagraphFactory());
        var fonts = new FontService(
            service,
            inline,
            new DocumentBackgroundPreferenceStoreStub(),
            new DocumentAppearanceDefaults());

        // FontService initializes defaults by changing the current caret. Restore the
        // exact document used by each test so initialization cannot mask a regression.
        paragraph = new Paragraph();
        foreach(var run in runs.Select(CloneRun))
            paragraph.Inlines.Add(run);
        service.Document.Blocks.Clear();
        service.Document.Blocks.Add(paragraph);
        service.CaretPosition = paragraph.ContentStart;
        service.ClearSelection();

        return (service, fonts);
    }

    private static (IRichTextBoxService Service, FontService Fonts) CreateInitialServices()
    {
        IRichTextBoxService service = new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());
        var inline = new InlineService(
            service,
            new ReflectionPropertyAccessor(),
            new TestParagraphFactory());
        var fonts = new FontService(
            service,
            inline,
            new DocumentBackgroundPreferenceStoreStub(),
            new DocumentAppearanceDefaults());

        return (service, fonts);
    }

    private static Run CloneRun(Run source) => new(source.Text)
    {
        FontFamily = source.FontFamily,
        FontSize = source.FontSize,
        FontWeight = source.FontWeight,
        FontStyle = source.FontStyle,
        FontStretch = source.FontStretch,
        Foreground = source.Foreground,
        Background = source.Background,
        TextDecorations = source.TextDecorations
    };

    private static BitmapSource CreateBitmap()
    {
        const int width = 2;
        const int height = 1;
        byte[] pixels =
        [
            0, 0, 255, 255,
            0, 255, 0, 255
        ];
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static (string Name, Action<FontService> Apply)[] FontPropertyChanges() =>
    [
        ("FontWeight", fonts => fonts.SetFontWeight(FontWeights.Bold)),
        ("FontStyle", fonts => fonts.SetFontStyle(FontStyles.Italic)),
        ("FontStretch", fonts => fonts.SetFontStretch(FontStretches.Expanded)),
        ("FontFamily", fonts => fonts.SetFontFamily(new FontFamily("Arial"))),
        ("FontSize", fonts => fonts.SetFontSize(22)),
        ("Foreground", fonts => fonts.SetFontColor(DrawingColor.Red)),
        ("Background", fonts => fonts.SetFontBackground(DrawingColor.Yellow)),
        ("TextDecorations", fonts => fonts.SetTextDecoration(TextDecorations.Underline))
    ];

    private static Run GetOnlyRun(IRichTextBoxService service) =>
        Assert.IsType<Run>(((Paragraph)service.Document.Blocks.FirstBlock!).Inlines.FirstInline);

    private static string GetText(IRichTextBoxService service) =>
        new TextRange(service.Document.ContentStart, service.Document.ContentEnd).Text.TrimEnd('\r', '\n');

    private static void AssertCharacterProperty(
        IRichTextBoxService service,
        int offset,
        DependencyProperty property,
        object expected)
    {
        var range = GetCharacterRange(service, offset);
        Assert.Equal(expected, range.GetPropertyValue(property));
    }

    private static void AssertCharacterBrush(
        IRichTextBoxService service,
        int offset,
        DependencyProperty property,
        System.Windows.Media.Color expected)
    {
        var value = Assert.IsType<SolidColorBrush>(GetCharacterRange(service, offset).GetPropertyValue(property));
        Assert.Equal(expected, value.Color);
    }

    private static TextRange GetCharacterRange(IRichTextBoxService service, int offset)
    {
        var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
        var position = paragraph.ContentStart;

        while(position != null && position.CompareTo(paragraph.ContentEnd) < 0)
        {
            if(position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = position.GetTextInRun(LogicalDirection.Forward);
                if(offset < text.Length)
                {
                    var start = position.GetPositionAtOffset(offset, LogicalDirection.Forward)!;
                    var end = start.GetPositionAtOffset(1, LogicalDirection.Forward)!;
                    return new TextRange(start, end);
                }

                offset -= text.Length;
                position = position.GetPositionAtOffset(text.Length, LogicalDirection.Forward);
                continue;
            }

            position = position.GetNextContextPosition(LogicalDirection.Forward);
        }

        throw new ArgumentOutOfRangeException(nameof(offset));
    }

    private static int GetTextOffset(Paragraph paragraph, TextPointer position) =>
        new TextRange(paragraph.ContentStart, position).Text.Length;

    private static void RaisePreviewTextInput(
        IRichTextBoxService service,
        string text)
    {
        var composition = new System.Windows.Input.TextComposition(
            System.Windows.Input.InputManager.Current,
            service.Service,
            text);
        var args = new System.Windows.Input.TextCompositionEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice,
            composition)
        {
            RoutedEvent = System.Windows.Input.TextCompositionManager.PreviewTextInputEvent
        };

        service.Service.RaiseEvent(args);
        Assert.True(args.Handled);
    }

    private sealed class TestParagraphFactory: IParagraphFactory
    {
        public IParagraphService Create(Inline? inline = null)
        {
            var paragraph = new ParagraphService();
            if(inline != null)
                paragraph.Inlines.Add(inline);
            return paragraph;
        }
    }

    private sealed class DocumentBackgroundPreferenceStoreStub:
        IDocumentBackgroundPreferenceStore
    {
        public DrawingColor? Load() => null;

        public void Save(DrawingColor color)
        {
        }
    }

    private sealed class ImageFilePickerStub(string selectedPath):
        IImageFilePicker
    {
        public Task<string?> PickImageAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(selectedPath);
        }
    }

    private sealed class ImageContentLoaderStub(BitmapSource image):
        IImageContentLoader
    {
        public Task<BitmapSource> LoadFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("background.png", filePath);
            return Task.FromResult(image);
        }

        public Task<BitmapSource> LoadAsync(
            Stream source,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

}

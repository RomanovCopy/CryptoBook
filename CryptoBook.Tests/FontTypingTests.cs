using CryptoBook.Accessors;
using CryptoBook.Interfaces;
using CryptoBook.Markup;
using CryptoBook.Services;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
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

}

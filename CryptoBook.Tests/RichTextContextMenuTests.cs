using CryptoBook.Accessors;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using System.Windows;
using System.Windows.Documents;
using Xunit;

namespace CryptoBook.Tests;

public sealed class RichTextContextMenuTests
{
    [WpfFact]
    public void ContextMenu_ReusesExistingFormattingViewModels()
    {
        var context = CreateContext("ab");

        Assert.Same(context.FontFormatting, context.ViewModel.FontFormatting);
        Assert.Same(context.TextFormatting, context.ViewModel.TextFormatting);
        Assert.Same(context.ListFormatting, context.ViewModel.ListFormatting);

        var run = GetOnlyRun(context.Service);
        context.Service.Selection.Select(
            run.ContentStart.GetPositionAtOffset(1)!,
            run.ContentEnd);

        context.ViewModel.FontFormatting.SetFontWeightCommand.Execute(context.ViewModel.Bold);
        context.ViewModel.FontFormatting.SetFontStyleCommand.Execute(context.ViewModel.Italic);
        context.ViewModel.FontFormatting.SetTextDecorationCommand.Execute(context.ViewModel.Underline);

        AssertCharacterProperty(context.Service, 0, TextElement.FontWeightProperty, FontWeights.Normal);
        AssertCharacterProperty(context.Service, 1, TextElement.FontWeightProperty, FontWeights.Bold);
        AssertCharacterProperty(context.Service, 1, TextElement.FontStyleProperty, FontStyles.Italic);
        Assert.Contains(
            TextDecorations.Underline[0],
            Assert.IsType<TextDecorationCollection>(
                GetCharacterRange(context.Service, 1).GetPropertyValue(Inline.TextDecorationsProperty)));
    }

    [WpfFact]
    public void AlignmentAndListCommands_UseExistingBarCommands()
    {
        var context = CreateContext("text");

        context.ViewModel.TextFormatting.SetTextAlignment.Execute(TextAlignment.Center);
        context.ViewModel.ListFormatting.ToggleBulleted.Execute(null);
        context.ViewModel.ListFormatting.ToggleNumbered.Execute(null);
        context.ListService.CanClearValue = true;
        context.ViewModel.ListFormatting.ClearLists.Execute(null);

        Assert.Equal(TextAlignment.Center, context.TextFormatService.LastAlignment);
        Assert.Equal(1, context.ListService.BulletedCalls);
        Assert.Equal(1, context.ListService.NumberedCalls);
        Assert.Equal(1, context.ListService.ClearCalls);
    }

    [WpfFact]
    public void ClearDocument_RemainsASeparateDocumentLevelCommand()
    {
        var context = CreateContext("document content");
        var oldParagraph =
            (Paragraph)context.Service.Document.Blocks.FirstBlock!;
        oldParagraph.LineHeight = 96;
        oldParagraph.LineStackingStrategy =
            LineStackingStrategy.BlockLineHeight;
        context.Service.Document.LineHeight = 96;

        Assert.True(context.ViewModel.ClearDocument.CanExecute(null));

        context.ViewModel.ClearDocument.Execute(null);

        Assert.Equal(
            string.Empty,
            new TextRange(
                context.Service.Document.ContentStart,
                context.Service.Document.ContentEnd).Text.TrimEnd('\r', '\n'));
        var clearedParagraph =
            Assert.IsAssignableFrom<Paragraph>(
                context.Service.Document.Blocks.FirstBlock);
        Assert.NotSame(oldParagraph, clearedParagraph);
        Assert.True(double.IsNaN(clearedParagraph.LineHeight));
        Assert.Equal(
            LineStackingStrategy.MaxHeight,
            clearedParagraph.LineStackingStrategy);
        Assert.True(double.IsNaN(context.Service.Document.LineHeight));
        Assert.False(context.ViewModel.ClearDocument.CanExecute(null));
    }

    [WpfFact]
    public void RemoveEmptyParagraphs_PreservesContentAndImages()
    {
        var context = CreateContext("content");
        var document = context.Service.Document;
        var contentParagraph =
            (Paragraph)document.Blocks.FirstBlock!;
        var empty = new Paragraph();
        var whitespace = new Paragraph(
            new Run(" \t\u200B "));
        var imageParagraph = new Paragraph(
            new InlineUIContainer(new System.Windows.Controls.Image()));
        var namedAnchor = new Paragraph
        {
            Name = "bookmark_target"
        };
        document.Blocks.InsertBefore(contentParagraph, empty);
        document.Blocks.InsertAfter(empty, whitespace);
        document.Blocks.InsertAfter(
            contentParagraph,
            imageParagraph);
        document.Blocks.InsertAfter(imageParagraph, namedAnchor);
        context.Service.CaretPosition = whitespace.ContentStart;

        Assert.True(
            context.ViewModel.RemoveEmptyParagraphs.CanExecute(null));

        context.ViewModel.RemoveEmptyParagraphs.Execute(null);

        Assert.Equal(3, document.Blocks.Count);
        Assert.Contains(contentParagraph, document.Blocks);
        Assert.Contains(imageParagraph, document.Blocks);
        Assert.Contains(namedAnchor, document.Blocks);
        Assert.NotNull(context.Service.CaretPosition.Paragraph);
        Assert.False(
            context.ViewModel.RemoveEmptyParagraphs.CanExecute(null));
    }

    [WpfFact]
    public void RemoveEmptyParagraphs_LeavesOneEditableParagraph()
    {
        var context = CreateContext(string.Empty);
        context.Service.Document.Blocks.Add(new Paragraph());
        context.Service.Document.Blocks.Add(
            new Paragraph(new Run("\u200B")));

        int removed = context.Service.RemoveEmptyParagraphs();

        Assert.Equal(2, removed);
        Assert.Single(context.Service.Document.Blocks);
        Assert.IsAssignableFrom<Paragraph>(
            context.Service.Document.Blocks.FirstBlock);
        Assert.NotNull(context.Service.CaretPosition.Paragraph);
    }

    private static TestContext CreateContext(string text)
    {
        var paragraphFactory = new TestParagraphFactory();
        IRichTextBoxService service = new RichTextBoxService(
            paragraphFactory,
            new TestUriNavigationService());
        var inline = new InlineService(service, new ReflectionPropertyAccessor(), paragraphFactory);
        var fonts = new FontService(
            service,
            inline,
            new DocumentBackgroundPreferenceStoreStub());
        var textFormatService = new RecordingTextFormatService();
        var listService = new RecordingListService();

        IFontFormatBar_ViewModel fontFormatting =
            new FontFormatBar_ViewModel(fonts, inline, service);
        ITextFormatBarViewModel textFormatting =
            new TextFormatBarViewModel(service, textFormatService);
        IListFormatBarViewModel listFormatting =
            new ListFormatBarViewModel(listService);
        var viewModel = new RichTextContextMenuViewModel(
            service,
            fontFormatting,
            textFormatting,
            listFormatting);

        var paragraph = new Paragraph(new Run(text));
        service.Document.Blocks.Clear();
        service.Document.Blocks.Add(paragraph);
        service.CaretPosition = paragraph.ContentStart;
        service.ClearSelection();

        return new TestContext(
            service,
            viewModel,
            fontFormatting,
            textFormatting,
            listFormatting,
            textFormatService,
            listService);
    }

    private static Run GetOnlyRun(IRichTextBoxService service) =>
        Assert.IsType<Run>(((Paragraph)service.Document.Blocks.FirstBlock!).Inlines.FirstInline);

    private static void AssertCharacterProperty(
        IRichTextBoxService service,
        int offset,
        DependencyProperty property,
        object expected) =>
        Assert.Equal(expected, GetCharacterRange(service, offset).GetPropertyValue(property));

    private static TextRange GetCharacterRange(IRichTextBoxService service, int offset)
    {
        var paragraph = (Paragraph)service.Document.Blocks.FirstBlock!;
        foreach(var run in paragraph.Inlines.OfType<Run>())
        {
            if(offset < run.Text.Length)
            {
                var start = run.ContentStart.GetPositionAtOffset(offset)!;
                var end = start.GetPositionAtOffset(1)!;
                return new TextRange(start, end);
            }
            offset -= run.Text.Length;
        }

        throw new ArgumentOutOfRangeException(nameof(offset));
    }

    private sealed record TestContext(
        IRichTextBoxService Service,
        RichTextContextMenuViewModel ViewModel,
        IFontFormatBar_ViewModel FontFormatting,
        ITextFormatBarViewModel TextFormatting,
        IListFormatBarViewModel ListFormatting,
        RecordingTextFormatService TextFormatService,
        RecordingListService ListService);

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

    private sealed class RecordingListService: IListService
    {
        public bool CanToggle => true;
        public bool CanClear => CanClearValue;
        public bool CanClearValue { get; set; }
        public int BulletedCalls { get; private set; }
        public int NumberedCalls { get; private set; }
        public int ClearCalls { get; private set; }

        public void ToggleBulleted() => BulletedCalls++;
        public void ToggleNumbered(int startIndex = 1) => NumberedCalls++;
        public void ClearLists() => ClearCalls++;
    }

    private sealed class DocumentBackgroundPreferenceStoreStub:
        IDocumentBackgroundPreferenceStore
    {
        public System.Drawing.Color? Load() => null;

        public void Save(System.Drawing.Color color)
        {
        }
    }

    private sealed class RecordingTextFormatService: ITextFormatService
    {
        public TextAlignment? LastAlignment { get; private set; }
        public double LineHeight { get; set; }
        public bool CanUndo => false;
        public bool CanRedo => false;

        public void SetTextAlignment(TextAlignment? alignment) => LastAlignment = alignment;
        public void SetParagraphIndent(double indent) => throw new NotSupportedException();
        public void SetLineHeight(double lineHeight) => throw new NotSupportedException();
        public void SetLineSpacing(double spacing) => throw new NotSupportedException();
        public void ToggleBulletList() => throw new NotSupportedException();
        public void ToggleNumberedList() => throw new NotSupportedException();
        public void InsertHyperlink(string url, string displayText) => throw new NotSupportedException();
        public void ClearAllFormatting() => throw new NotSupportedException();
        public TextRange GetSelectedTextRange() => throw new NotSupportedException();
        public void ReplaceSelectedText(string newText) => throw new NotSupportedException();
        public void Undo() => throw new NotSupportedException();
        public void Redo() => throw new NotSupportedException();
        public void MoveCaretToStart() => throw new NotSupportedException();
        public void MoveCaretToEnd() => throw new NotSupportedException();
    }
}

using CryptoBook.Composition;
using CryptoBook.Behaviors;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Xunit;

namespace CryptoBook.Tests;

public sealed class TextFormattingTests
{
    [WpfFact]
    public void AlignmentAndIndent_WorkAtCaretWithoutSelection()
    {
        var (richText, formatting) = CreateDocument("first", "second");
        var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
        richText.CaretPosition = first.ContentStart;
        richText.ClearSelection();

        formatting.SetTextAlignment(TextAlignment.Center);
        formatting.SetParagraphIndent(20);

        Assert.Equal(TextAlignment.Center, first.TextAlignment);
        Assert.Equal(20, first.TextIndent);
        Assert.Equal(2, richText.Document.Blocks.Count);
    }

    [WpfFact]
    public void ParagraphFormatting_AppliesToEverySelectedParagraph()
    {
        var (richText, formatting) = CreateDocument("first", "second");
        var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var second = (Paragraph)richText.Document.Blocks.LastBlock!;
        richText.Selection.Select(first.ContentStart, second.ContentEnd);

        formatting.SetTextAlignment(TextAlignment.Right);
        formatting.SetParagraphIndent(15);
        formatting.SetLineHeight(30);

        foreach(var paragraph in new[] { first, second })
        {
            Assert.Equal(TextAlignment.Right, paragraph.TextAlignment);
            Assert.Equal(15, paragraph.TextIndent);
            Assert.Equal(30, paragraph.LineHeight);
            Assert.Equal(LineStackingStrategy.MaxHeight, paragraph.LineStackingStrategy);
        }
    }

    [WpfFact]
    public void OrdinaryTextDocument_ParagraphFormattingIgnoresSelection()
    {
        foreach(IFileTemplate template in new IFileTemplate[]
                {
                    new PlainTextTemplate(),
                    new XamlFileTemplate()
                })
        {
            var (richText, _) = CreateDocument("first", "second");
            var session = new DocumentSession(richText);
            session.Open(
                Path.Combine(Path.GetTempPath(), "document" + template.DefaultExtension),
                template);
            var formatting = new TextFormatService(
                richText,
                new LineSpacingPreferenceStoreStub(),
                new DocumentLineSpacing(),
                session);
            var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
            var second = (Paragraph)richText.Document.Blocks.LastBlock!;
            richText.Selection.Select(first.ContentStart, first.ContentEnd);

            formatting.SetTextAlignment(TextAlignment.Right);
            formatting.SetParagraphIndent(15);
            formatting.SetLineHeight(30);

            foreach(var paragraph in new[] { first, second })
            {
                Assert.Equal(TextAlignment.Right, paragraph.TextAlignment);
                Assert.Equal(15, paragraph.TextIndent);
                Assert.Equal(30, paragraph.LineHeight);
            }
        }
    }

    [WpfFact]
    public void LineHeightButtons_CanReduceBelowNaturalHeightWithinSafeBounds()
    {
        var preferences = new LineSpacingPreferenceStoreStub();
        var (richText, formatting) = CreateDocument(
            preferences,
            "text");
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        paragraph.FontSize = 20;
        richText.CaretPosition = paragraph.ContentStart;
        richText.ClearSelection();

        formatting.SetLineHeight(-1);
        Assert.Equal(22, paragraph.LineHeight, 5);
        Assert.Equal(
            LineStackingStrategy.BlockLineHeight,
            paragraph.LineStackingStrategy);

        formatting.SetLineHeight(-1);
        Assert.Equal(20, paragraph.LineHeight, 5);

        formatting.SetLineHeight(-1);
        Assert.Equal(18, paragraph.LineHeight, 5);

        formatting.SetLineHeight(-1);
        formatting.SetLineHeight(-1);
        Assert.Equal(16, paragraph.LineHeight, 5);
        Assert.Equal(0.8, preferences.Ratio, 5);

        formatting.SetLineHeight(1);
        Assert.Equal(18, paragraph.LineHeight, 5);

        formatting.SetLineHeight(1);
        formatting.SetLineHeight(1);
        Assert.Equal(22, paragraph.LineHeight, 5);

        formatting.SetLineHeight(1);
        Assert.Equal(24, paragraph.LineHeight, 5);
        Assert.Equal(
            LineStackingStrategy.MaxHeight,
            paragraph.LineStackingStrategy);

        formatting.SetLineHeight(1);
        Assert.Equal(26, paragraph.LineHeight, 5);
    }

    [WpfFact]
    public void MinimumToolbarLineHeight_DoesNotOverlapAdjacentParagraphs()
    {
        var (richText, formatting) = CreateDocument("one", "two");
        var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var second = (Paragraph)richText.Document.Blocks.LastBlock!;
        first.FontSize = 20;
        second.FontSize = 20;
        richText.Selection.Select(first.ContentStart, second.ContentEnd);

        for(var step = 0; step < 4; step++)
            formatting.SetLineHeight(-1);

        Assert.Equal(16, first.LineHeight, 5);
        Assert.Equal(16, second.LineHeight, 5);

        var host = new Window
        {
            Content = richText.Service,
            Width = 400,
            Height = 250
        };
        host.Show();
        try
        {
            richText.Service.UpdateLayout();
            var firstRect = first.ContentStart.GetCharacterRect(
                LogicalDirection.Forward);
            var secondRect = second.ContentStart.GetCharacterRect(
                LogicalDirection.Forward);

            Assert.True(
                secondRect.Top >= firstRect.Bottom - 0.5,
                $"Compact paragraph rectangles overlap: first={firstRect}, second={secondRect}.");
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void ListCommands_WorkForCurrentParagraphAndCanSwitchMarker()
    {
        var (richText, _) = CreateDocument("item");
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        richText.CaretPosition = paragraph.ContentStart;
        richText.ClearSelection();
        var lists = new ListService(
            new DocumentSelection(richText),
            new EditTransaction(richText));

        Assert.True(lists.CanToggle);
        lists.ToggleBulleted();

        var list = Assert.IsType<List>(richText.Document.Blocks.FirstBlock);
        Assert.Equal(TextMarkerStyle.Disc, list.MarkerStyle);

        lists.ToggleNumbered();
        list = Assert.IsType<List>(richText.Document.Blocks.FirstBlock);
        Assert.Equal(TextMarkerStyle.Decimal, list.MarkerStyle);
        Assert.IsNotType<List>(list.ListItems.FirstListItem!.Blocks.FirstBlock);

        lists.ClearLists();
        Assert.IsType<Paragraph>(richText.Document.Blocks.FirstBlock);
    }

    [WpfFact]
    public void ListCommand_GroupsSelectedParagraphsAndTogglesOff()
    {
        var (richText, _) = CreateDocument("one", "two");
        var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var second = (Paragraph)richText.Document.Blocks.LastBlock!;
        richText.Selection.Select(first.ContentStart, second.ContentEnd);
        var lists = new ListService(
            new DocumentSelection(richText),
            new EditTransaction(richText));

        lists.ToggleBulleted();

        var list = Assert.IsType<List>(richText.Document.Blocks.FirstBlock);
        Assert.Equal(2, list.ListItems.Count);

        lists.ToggleBulleted();
        Assert.Equal(2, richText.Document.Blocks.Count);
        Assert.All(richText.Document.Blocks.Cast<Block>(), block => Assert.IsType<Paragraph>(block));
    }

    [WpfFact]
    public void OrdinaryTextDocument_ListFormattingIgnoresSelection()
    {
        var (richText, _) = CreateDocument("one", "two");
        var session = new DocumentSession(richText);
        session.Open(
            Path.Combine(Path.GetTempPath(), "document.json"),
            new PlainTextTemplate());
        var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
        richText.Selection.Select(first.ContentStart, first.ContentEnd);
        var lists = new ListService(
            new DocumentSelection(richText, session),
            new EditTransaction(richText));

        lists.ToggleBulleted();

        var list = Assert.IsType<List>(richText.Document.Blocks.FirstBlock);
        Assert.Equal(2, list.ListItems.Count);
    }

    [WpfFact]
    public void EnterInsideList_CreatesNextListItem()
    {
        var (richText, _) = CreateDocument("first");
        var host = new Window { Content = richText.Service };
        host.Show();
        try
        {
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        richText.CaretPosition = paragraph.ContentEnd;
        richText.ClearSelection();
        var lists = new ListService(
            new DocumentSelection(richText),
            new EditTransaction(richText));
        lists.ToggleBulleted();

        paragraph = Assert.IsType<Paragraph>(
            Assert.IsType<List>(richText.Document.Blocks.FirstBlock)
                .ListItems.FirstListItem!.Blocks.FirstBlock);
        richText.CaretPosition = paragraph.ContentEnd;
        richText.ClearSelection();
        richText.Focus();

        Assert.True(EditingCommands.EnterParagraphBreak.CanExecute(null, richText.Service));
        EditingCommands.EnterParagraphBreak.Execute(null, richText.Service);

        var list = Assert.IsType<List>(richText.Document.Blocks.FirstBlock);
        Assert.Equal(2, list.ListItems.Count);
        Assert.Same(
            list.ListItems.LastListItem,
            richText.CaretPosition.Paragraph?.Parent);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void PlainEnter_IsLeftToTheStandardRichTextBoxListHandling()
    {
        var (richText, _) = CreateDocument("item");
        var host = new Window { Content = richText.Service };
        host.Show();
        try
        {
            richText.Focus();
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(host),
                Environment.TickCount,
                Key.Enter)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };

            richText.Service.RaiseEvent(args);

            Assert.False(args.Handled);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void Escape_DismissesSelectionAndPreventsItsRestoration()
    {
        var (richText, _) = CreateDocument("text");
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        richText.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
        richText.Service.RaiseEvent(
            new RoutedEventArgs(UIElement.LostFocusEvent));

        var host = new Window { Content = richText.Service };
        host.Show();
        try
        {
            richText.Focus();
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(host),
                Environment.TickCount,
                Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };

            richText.Service.RaiseEvent(args);
            richText.RestoreSelection();

            Assert.True(args.Handled);
            Assert.True(richText.Selection.IsEmpty);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void ClickPositionOutsideSelection_DismissesSavedSelection()
    {
        var (richText, _) = CreateDocument("text");
        var editor = Assert.IsType<RichTextBoxService>(richText.Service);
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var selectedEnd = paragraph.ContentStart.GetPositionAtOffset(1)!;
        richText.Selection.Select(paragraph.ContentStart, selectedEnd);
        richText.Service.RaiseEvent(
            new RoutedEventArgs(UIElement.LostFocusEvent));

        var clickPosition = paragraph.ContentEnd;
        editor.DismissSelectionIfOutside(clickPosition);
        richText.RestoreSelection();

        Assert.True(richText.Selection.IsEmpty);
    }

    [WpfFact]
    public void OrdinaryParagraphs_UseCompactAutomaticLineSpacing()
    {
        var (richText, _) = CreateDocument("first");
        var host = new Window
        {
            Content = richText.Service,
            Width = 400,
            Height = 250
        };
        host.Show();
        try
        {
            var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
            first.Margin = new Thickness(0);
            richText.CaretPosition = first.ContentEnd;
            richText.ClearSelection();
            richText.Focus();

            EditingCommands.EnterParagraphBreak.Execute(null, richText.Service);
            richText.Service.UpdateLayout();

            var second = Assert.IsType<Paragraph>(richText.Document.Blocks.LastBlock);
            Assert.Equal(new Thickness(0), first.Margin);
            Assert.Equal(new Thickness(0), second.Margin);
            Assert.True(double.IsNaN(richText.Document.LineHeight));
            Assert.Equal(LineStackingStrategy.MaxHeight, richText.Document.LineStackingStrategy);

            var firstRect = first.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            var secondRect = second.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            Assert.InRange(
                secondRect.Top - firstRect.Top,
                firstRect.Height,
                firstRect.Height * 1.6);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void SmallRequestedLineHeight_DoesNotOverlapListItems()
    {
        var (richText, formatting) = CreateDocument("one", "two");
        var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var second = (Paragraph)richText.Document.Blocks.LastBlock!;
        richText.Selection.Select(first.ContentStart, second.ContentEnd);
        var lists = new ListService(
            new DocumentSelection(richText),
            new EditTransaction(richText));
        lists.ToggleBulleted();
        formatting.SetLineHeight(8);

        var host = new Window
        {
            Content = richText.Service,
            Width = 400,
            Height = 250
        };
        host.Show();
        try
        {
            richText.Service.UpdateLayout();
            Assert.Equal(LineStackingStrategy.MaxHeight, first.LineStackingStrategy);
            Assert.Equal(LineStackingStrategy.MaxHeight, second.LineStackingStrategy);

            var firstRect = first.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            var secondRect = second.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            Assert.True(
                secondRect.Top >= firstRect.Bottom - 0.5,
                $"List item rectangles overlap: first={firstRect}, second={secondRect}.");
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void ClearAllFormatting_ResetsCharacterAndParagraphProperties()
    {
        var (richText, formatting) = CreateDocument("text");
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var run = (Run)paragraph.Inlines.FirstInline!;
        run.FontWeight = FontWeights.Bold;
        paragraph.TextAlignment = TextAlignment.Right;
        paragraph.TextIndent = 20;
        paragraph.LineHeight = 30;
        richText.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);

        formatting.ClearAllFormatting();

        Assert.Equal(FontWeights.Normal, run.FontWeight);
        Assert.Equal(TextAlignment.Left, paragraph.TextAlignment);
        Assert.Equal(0, paragraph.TextIndent);
        Assert.Same(
            DependencyProperty.UnsetValue,
            paragraph.ReadLocalValue(Paragraph.LineHeightProperty));
        Assert.Equal(LineStackingStrategy.MaxHeight, paragraph.LineStackingStrategy);
    }

    [WpfFact]
    public void SelectionReplacementAndNavigation_PreserveDocumentConsistency()
    {
        var (richText, formatting) = CreateDocument("hello");
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var run = (Run)paragraph.Inlines.FirstInline!;
        richText.Selection.Select(
            run.ContentStart.GetPositionAtOffset(1)!,
            run.ContentStart.GetPositionAtOffset(4)!);

        Assert.Equal("ell", formatting.GetSelectedTextRange().Text);
        formatting.ReplaceSelectedText("i");
        Assert.Equal(
            "hio",
            new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);

        formatting.MoveCaretToStart();
        Assert.Equal(
            0,
            richText.Document.ContentStart
                .GetInsertionPosition(LogicalDirection.Forward)
                .CompareTo(richText.CaretPosition));
        formatting.MoveCaretToEnd();
        Assert.Equal(
            0,
            richText.Document.ContentEnd
                .GetInsertionPosition(LogicalDirection.Backward)
                .CompareTo(richText.CaretPosition));
    }

    [WpfFact]
    public void HyperlinkInsertion_SplitsAdjacentTextAtCaret()
    {
        var (richText, formatting) = CreateDocument("ab");
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var run = (Run)paragraph.Inlines.FirstInline!;
        richText.CaretPosition = run.ContentStart.GetPositionAtOffset(1)!;
        richText.ClearSelection();

        formatting.InsertHyperlink("https://example.com", "X");

        Assert.Equal("aXb", new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
        var link = Assert.Single(paragraph.Inlines.OfType<Hyperlink>());
        Assert.Equal(new Uri("https://example.com"), link.NavigateUri);
        Assert.Equal(System.Windows.Input.Cursors.Hand, link.Cursor);
        Assert.True(richText.Service.IsDocumentEnabled);
    }

    [WpfFact]
    public void HyperlinkInsertion_RejectsNonHttpSchemes()
    {
        var (richText, formatting) = CreateDocument("text");

        formatting.InsertHyperlink("file:///C:/secret.txt", "local");

        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        Assert.Empty(paragraph.Inlines.OfType<Hyperlink>());
    }

    [WpfFact]
    public void HyperlinkInsertion_DoesNotCreateNestedHyperlink()
    {
        var (richText, formatting) = CreateDocument("ab");
        var paragraph = (Paragraph)richText.Document.Blocks.FirstBlock!;
        var run = (Run)paragraph.Inlines.FirstInline!;
        richText.CaretPosition = run.ContentStart.GetPositionAtOffset(1)!;
        richText.ClearSelection();
        formatting.InsertHyperlink("https://example.com", "XY");
        var original = Assert.Single(paragraph.Inlines.OfType<Hyperlink>());
        var linkedRun = Assert.IsType<Run>(original.Inlines.FirstInline);
        richText.CaretPosition = linkedRun.ContentStart.GetPositionAtOffset(1)!;
        richText.ClearSelection();

        formatting.InsertHyperlink("https://openai.com", "nested");

        Assert.Single(paragraph.Inlines.OfType<Hyperlink>());
        Assert.Equal("aXYb", new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
    }

    [WpfFact]
    public void PreviewMode_UsesIndependentDocumentAndReturnsToEditing()
    {
        var (richText, _) = CreateDocument("preview text");
        var navigator = new StubUriNavigationService();
        var viewModel = new RichtextboxViewModel(
            new RichtextboxModel(),
            richText,
            new DocumentPreviewService(),
            navigator,
            new StubMenuFileViewModel());

        viewModel.ToggleView.Execute(null);

        Assert.True(viewModel.IsPreviewMode);
        Assert.True(viewModel.IsFitToWindow);
        Assert.True(viewModel.ToggleFitToWindow.CanExecute(null));
        Assert.NotNull(viewModel.PreviewDocument);
        Assert.NotSame(richText.Document, viewModel.PreviewDocument);
        Assert.Contains(
            "preview text",
            new TextRange(
                viewModel.PreviewDocument!.ContentStart,
                viewModel.PreviewDocument.ContentEnd).Text);

        viewModel.ToggleFitToWindow.Execute(null);

        Assert.False(viewModel.IsFitToWindow);
        Assert.Equal(
            LocalizationManager.GetString("Editor.FitToWindow"),
            viewModel.FitToWindowText);
        Assert.Equal("\uE740", viewModel.FitToWindowGlyph);

        viewModel.ToggleView.Execute(null);

        Assert.False(viewModel.IsPreviewMode);
        Assert.Null(viewModel.PreviewDocument);
    }

    [WpfFact]
    public void PreviewHyperlinkCommand_DelegatesToNavigationService()
    {
        var (richText, _) = CreateDocument("text");
        var navigator = new StubUriNavigationService();
        var viewModel = new RichtextboxViewModel(
            new RichtextboxModel(),
            richText,
            new DocumentPreviewService(),
            navigator,
            new StubMenuFileViewModel());
        var uri = new Uri("https://example.com");

        viewModel.OpenHyperlink.Execute(uri);

        Assert.Equal(uri, navigator.LastOpenedUri);
    }

    [WpfFact]
    public void HyperlinkCommandBehavior_RegistersCompatibleRoutedEventHandler()
    {
        var viewer = new FlowDocumentPageViewer();
        var command = new RelayCommand(_ => { });

        HyperlinkCommandBehavior.SetCommand(viewer, command);
        DocumentPageKeyboardNavigationBehavior.SetIsEnabled(viewer, true);
        Assert.True(DocumentPageKeyboardNavigationBehavior.GetIsEnabled(viewer));
        DocumentPageKeyboardNavigationBehavior.SetIsEnabled(viewer, false);
        HyperlinkCommandBehavior.SetCommand(viewer, null);
    }

    private static (IRichTextBoxService RichText, TextFormatService Formatting) CreateDocument(
        params string[] paragraphs)
        => CreateDocument(new LineSpacingPreferenceStoreStub(), paragraphs);

    private static (IRichTextBoxService RichText, TextFormatService Formatting) CreateDocument(
        IDocumentLineSpacingPreferenceStore preferenceStore,
        params string[] paragraphs)
    {
        IRichTextBoxService richText = new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());
        richText.Document.Blocks.Clear();
        foreach(var text in paragraphs)
            richText.Document.Blocks.Add(new Paragraph(new Run(text)));

        var first = (Paragraph)richText.Document.Blocks.FirstBlock!;
        richText.CaretPosition = first.ContentStart;
        richText.ClearSelection();
        return (richText, new TextFormatService(
            richText,
            preferenceStore,
            new DocumentLineSpacing()));
    }

    private sealed class LineSpacingPreferenceStoreStub:
        IDocumentLineSpacingPreferenceStore
    {
        public double Ratio { get; private set; } = 1.2;

        public double Load() => Ratio;

        public void Save(double ratio) => Ratio = ratio;
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

    private sealed class StubUriNavigationService: IUriNavigationService
    {
        public Uri? LastOpenedUri { get; private set; }

        public bool TryOpen(Uri uri)
        {
            LastOpenedUri = uri;
            return true;
        }
    }

    private sealed class StubMenuFileViewModel: IMenuFileViewModel
    {
        private static ICommand Command { get; } =
            new RelayCommand(_ => { });

        public ICommand NewFile => Command;
        public ICommand OpenFile => Command;
        public ICommand SaveFile => Command;
        public ICommand SaveAsFile => Command;
        public ICommand PrintFile => Command;
        public ICommand FileOverview => Command;
        public ICommand OpenDirectory => Command;
        public ICommand UpdateFile => Command;
        public ICommand CloseFile => Command;
        public ICommand WorkingDirectorySynchronization => Command;
    }
}

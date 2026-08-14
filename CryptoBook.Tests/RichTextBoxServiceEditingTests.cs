using CryptoBook.Accessors;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace CryptoBook.Tests;

public sealed class RichTextBoxServiceEditingTests
{
    [WpfFact]
    public void ReadOnly_WithPendingTypingProperty_DoesNotModifyDocument()
    {
        var service = CreateEditor("ab");
        var run = GetOnlyRun(service);
        MoveCaret(service, run.ContentStart.GetPositionAtOffset(1)!);
        service.SetTypingProperty(
            TextElement.FontWeightProperty,
            FontWeights.Bold);
        service.IsReadOnly = true;

        RaisePreviewTextInput(service, "X");

        Assert.Equal("ab", GetText(service));
    }

    [WpfFact]
    public void FormattedTyping_ReplacesActiveSelection()
    {
        var service = CreateEditor("abcd");
        var run = GetOnlyRun(service);
        var selectionEnd = run.ContentStart.GetPositionAtOffset(2)!;
        MoveCaret(service, selectionEnd);
        service.SetTypingProperty(
            TextElement.FontWeightProperty,
            FontWeights.Bold);
        service.Selection.Select(run.ContentStart, selectionEnd);

        RaisePreviewTextInput(service, "X");

        Assert.Equal("Xcd", GetText(service));
        AssertCharacterProperty(
            service,
            0,
            TextElement.FontWeightProperty,
            FontWeights.Bold);
    }

    [WpfFact]
    public void FormattedTyping_UndoAndRedoTreatInputAsSingleUnit()
    {
        var service = CreateEditor("ab");
        var host = ShowEditor(service);
        try
        {
            var run = GetOnlyRun(service);
            MoveCaret(service, run.ContentStart.GetPositionAtOffset(1)!);
            service.Focus();
            ResetUndoHistory(service);
            service.SetTypingProperty(
                TextElement.FontWeightProperty,
                FontWeights.Bold);
            service.SetTypingProperty(
                TextElement.FontStyleProperty,
                FontStyles.Italic);

            service.InsertTextAtCaret("X");
            Assert.True(service.CanUndo);

            service.Undo();

            Assert.Equal("ab", GetText(service));
            Assert.True(service.CanRedo);

            service.Redo();

            Assert.Equal("aXb", GetText(service));
            AssertCharacterProperty(
                service,
                1,
                TextElement.FontWeightProperty,
                FontWeights.Bold);
            AssertCharacterProperty(
                service,
                1,
                TextElement.FontStyleProperty,
                FontStyles.Italic);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void FormattedTyping_CreatesSingleUndoRecord()
    {
        var service = CreateEditor("ab");
        var host = ShowEditor(service);
        try
        {
            var run = GetOnlyRun(service);
            MoveCaret(service, run.ContentStart.GetPositionAtOffset(1)!);
            service.Focus();
            ResetUndoHistory(service);
            service.SetTypingProperty(
                TextElement.FontWeightProperty,
                FontWeights.Bold);
            service.SetTypingProperty(
                TextElement.FontStyleProperty,
                FontStyles.Italic);
            service.InsertTextAtCaret("X");

            var undoCount = 0;
            while(service.CanUndo && undoCount < 32)
            {
                service.Undo();
                undoCount++;
            }

            Assert.Equal("ab", GetText(service));
            Assert.True(
                undoCount == 1,
                $"One formatted input created {undoCount} undo records instead of one.");
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void FormattedTyping_RedoRestoresCharacterAndPropertiesInOneStep()
    {
        var service = CreateEditor("ab");
        var host = ShowEditor(service);
        try
        {
            var run = GetOnlyRun(service);
            MoveCaret(service, run.ContentStart.GetPositionAtOffset(1)!);
            service.Focus();
            ResetUndoHistory(service);
            service.SetTypingProperty(
                TextElement.FontWeightProperty,
                FontWeights.Bold);
            service.SetTypingProperty(
                TextElement.FontStyleProperty,
                FontStyles.Italic);
            service.InsertTextAtCaret("X");

            var undoCount = 0;
            while(service.CanUndo && undoCount < 32)
            {
                service.Undo();
                undoCount++;
            }

            Assert.Equal("ab", GetText(service));
            Assert.True(service.CanRedo);

            service.Redo();

            Assert.Equal("aXb", GetText(service));
            AssertCharacterProperty(
                service,
                1,
                TextElement.FontWeightProperty,
                FontWeights.Bold);
            AssertCharacterProperty(
                service,
                1,
                TextElement.FontStyleProperty,
                FontStyles.Italic);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void TypingProperty_PersistsAcrossParagraphBreak()
    {
        var service = CreateEditor("a");
        var host = ShowEditor(service);
        try
        {
            var run = GetOnlyRun(service);
            MoveCaret(service, run.ContentEnd);
            service.Focus();
            service.SetTypingProperty(
                TextElement.FontWeightProperty,
                FontWeights.Bold);

            EditingCommands.EnterParagraphBreak.Execute(
                null,
                service.Service);
            service.InsertTextAtCaret("X");

            var paragraph = Assert.IsType<Paragraph>(
                service.Document.Blocks.LastBlock);
            Assert.Equal("X", GetText(paragraph));
            Assert.Equal(
                FontWeights.Bold,
                GetRunContaining(paragraph, "X").FontWeight);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void TypingProperty_PersistsAcrossLineBreak()
    {
        var service = CreateEditor("a");
        var host = ShowEditor(service);
        try
        {
            var run = GetOnlyRun(service);
            var paragraph = Assert.IsType<Paragraph>(
                service.Document.Blocks.FirstBlock);
            MoveCaret(service, run.ContentEnd);
            service.Focus();
            service.SetTypingProperty(
                TextElement.FontWeightProperty,
                FontWeights.Bold);

            EditingCommands.EnterLineBreak.Execute(null, service.Service);
            service.InsertTextAtCaret("X");

            Assert.Equal("a\r\nX", GetText(paragraph));
            Assert.Equal(
                FontWeights.Bold,
                GetRunContaining(paragraph, "X").FontWeight);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void TypingProperty_PersistsAfterBackspaceCorrection()
    {
        var service = CreateEditor("ab");
        var host = ShowEditor(service);
        try
        {
            var run = GetOnlyRun(service);
            MoveCaret(service, run.ContentStart.GetPositionAtOffset(1)!);
            service.Focus();
            service.SetTypingProperty(
                TextElement.FontWeightProperty,
                FontWeights.Bold);
            service.InsertTextAtCaret("X");

            EditingCommands.Backspace.Execute(null, service.Service);
            service.InsertTextAtCaret("Y");

            Assert.Equal("aYb", GetText(service));
            AssertCharacterProperty(
                service,
                1,
                TextElement.FontWeightProperty,
                FontWeights.Bold);
        }
        finally
        {
            host.Close();
        }
    }

    [WpfFact]
    public void CaretNavigation_DoesNotReactivateStaleTypingProperties()
    {
        var service = CreateEditor("abc");
        var run = GetOnlyRun(service);
        var originalPosition = run.ContentStart.GetPositionAtOffset(1)!;
        MoveCaret(service, originalPosition);
        service.SetTypingProperty(
            TextElement.FontWeightProperty,
            FontWeights.Bold);

        MoveCaret(service, run.ContentEnd);
        MoveCaret(service, originalPosition);
        service.InsertTextAtCaret("X");

        Assert.Equal("aXbc", GetText(service));
        AssertCharacterProperty(
            service,
            1,
            TextElement.FontWeightProperty,
            FontWeights.Normal);
    }

    [WpfFact]
    public void ConsecutiveFormattedInput_UsesSingleFormattingRun()
    {
        var service = CreateEditor("ab");
        var run = GetOnlyRun(service);
        MoveCaret(service, run.ContentStart.GetPositionAtOffset(1)!);
        service.SetTypingProperty(
            TextElement.FontWeightProperty,
            FontWeights.Bold);

        service.InsertTextAtCaret("X");
        service.InsertTextAtCaret("Y");

        Assert.Equal("aXYb", GetText(service));
        var paragraph = Assert.IsType<Paragraph>(
            service.Document.Blocks.FirstBlock);
        var formattedRun = Assert.Single(
            paragraph.Inlines.OfType<Run>(),
            item => item.FontWeight == FontWeights.Bold);
        Assert.Equal("XY", formattedRun.Text);
    }

    [WpfFact]
    public void InitialDocument_HasNoUserText()
    {
        var service = CreateEditor();

        Assert.Equal(string.Empty, GetText(service));
    }

    [WpfFact]
    public void MultiCharacterComposition_PreservesTextAndFormatting()
    {
        var service = CreateEditor("ab");
        var run = GetOnlyRun(service);
        MoveCaret(service, run.ContentStart.GetPositionAtOffset(1)!);
        service.SetTypingProperty(
            TextElement.FontWeightProperty,
            FontWeights.Bold);

        RaisePreviewTextInput(service, "Ж😀");

        Assert.Equal("aЖ😀b", GetText(service));
        var paragraph = Assert.IsType<Paragraph>(
            service.Document.Blocks.FirstBlock);
        Assert.Equal(
            FontWeights.Bold,
            GetRunContaining(paragraph, "Ж😀").FontWeight);
    }

    [WpfFact]
    public void OpeningFontToolbar_DoesNotReplaceSelectedTextProperties()
    {
        IRichTextBoxService service = new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());
        var previous = new Run("previous")
        {
            FontFamily = new FontFamily("Arial"),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Normal,
            FontStretch = FontStretches.Condensed,
            Foreground = Brushes.Red,
            Background = Brushes.Yellow,
            TextDecorations = System.Windows.TextDecorations.Strikethrough
        };
        var selected = new Run("selected")
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 24,
            FontWeight = FontWeights.Light,
            FontStyle = FontStyles.Italic,
            FontStretch = FontStretches.Expanded,
            Foreground = Brushes.Blue,
            Background = Brushes.Green,
            TextDecorations = System.Windows.TextDecorations.Underline
        };
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(previous);
        paragraph.Inlines.Add(selected);
        service.Document.Blocks.Clear();
        service.Document.Blocks.Add(paragraph);
        MoveCaret(service, previous.ContentEnd);

        var inline = new InlineService(
            service,
            new ReflectionPropertyAccessor(),
            new TestParagraphFactory());
        var fonts = new FontService(
            service,
            inline,
            new DocumentBackgroundPreferenceStoreStub(),
            new DocumentAppearanceDefaults());
        var viewModel = new FontFormatBar_ViewModel(
            fonts,
            inline,
            service);
        viewModel.PropertyChanged += (_, args) =>
        {
            switch(args.PropertyName)
            {
                case nameof(viewModel.FontFamily):
                    viewModel.SetFontFamilyCommand.Execute(viewModel.FontFamily);
                    break;
                case nameof(viewModel.FontSize):
                    viewModel.SetFontSizeCommand.Execute(viewModel.FontSize);
                    break;
                case nameof(viewModel.FontWeight):
                    viewModel.SetFontWeightCommand.Execute(viewModel.FontWeight);
                    break;
                case nameof(viewModel.FontStyle):
                    viewModel.SetFontStyleCommand.Execute(viewModel.FontStyle);
                    break;
                case nameof(viewModel.FontStretch):
                    viewModel.SetFontStretchCommand.Execute(viewModel.FontStretch);
                    break;
                case nameof(viewModel.FontColor):
                    viewModel.SetFontColorCommand.Execute(viewModel.FontColor);
                    break;
                case nameof(viewModel.FontBackground):
                    viewModel.SetFontBackgroundCommand.Execute(
                        viewModel.FontBackground);
                    break;
                case nameof(viewModel.TextDecoration):
                    viewModel.SetTextDecorationCommand.Execute(
                        viewModel.TextDecoration);
                    break;
            }
        };

        service.Selection.Select(selected.ContentStart, selected.ContentEnd);
        service.Service.RaiseEvent(
            new RoutedEventArgs(UIElement.LostFocusEvent));
        var previousPosition = previous.ContentStart.GetPositionAtOffset(1)!;
        service.CaretPosition = previousPosition;
        service.Selection.Select(previousPosition, previousPosition);

        viewModel.Opened.Execute(null);

        Assert.Equal(new FontFamily("Consolas"), selected.FontFamily);
        Assert.Equal(24, selected.FontSize);
        Assert.Equal(FontWeights.Light, selected.FontWeight);
        Assert.Equal(FontStyles.Italic, selected.FontStyle);
        Assert.Equal(FontStretches.Expanded, selected.FontStretch);
        Assert.Equal(Colors.Blue, Assert.IsType<SolidColorBrush>(selected.Foreground).Color);
        Assert.Equal(Colors.Green, Assert.IsType<SolidColorBrush>(selected.Background).Color);
        Assert.Contains(
            System.Windows.TextDecorations.Underline[0],
            selected.TextDecorations);
        Assert.Equal(new FontFamily("Consolas"), viewModel.FontFamily);
        Assert.Equal(24, viewModel.FontSize);
        Assert.Equal(FontWeights.Light, viewModel.FontWeight);

        viewModel.SetFontWeightCommand.Execute(FontWeights.Black);

        Assert.Equal(FontWeights.Black, selected.FontWeight);
    }

    private static IRichTextBoxService CreateEditor(string? text = null)
    {
        IRichTextBoxService service = new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());

        if(text == null)
            return service;

        var paragraph = new Paragraph(new Run(text));
        service.Document.Blocks.Clear();
        service.Document.Blocks.Add(paragraph);
        MoveCaret(service, paragraph.ContentStart);
        return service;
    }

    private static Window ShowEditor(IRichTextBoxService service)
    {
        var host = new Window
        {
            Content = service.Service,
            Width = 400,
            Height = 250
        };
        host.Show();
        service.Service.UpdateLayout();
        return host;
    }

    private static void ResetUndoHistory(IRichTextBoxService service)
    {
        service.Service.IsUndoEnabled = false;
        service.Service.IsUndoEnabled = true;
    }

    private static void MoveCaret(
        IRichTextBoxService service,
        TextPointer position)
    {
        service.CaretPosition = position;
        service.ClearSelection();
    }

    private static TextCompositionEventArgs RaisePreviewTextInput(
        IRichTextBoxService service,
        string text)
    {
        var composition = new TextComposition(
            InputManager.Current,
            service.Service,
            text);
        var args = new TextCompositionEventArgs(
            Keyboard.PrimaryDevice,
            composition)
        {
            RoutedEvent = TextCompositionManager.PreviewTextInputEvent
        };

        service.Service.RaiseEvent(args);
        return args;
    }

    private static Run GetOnlyRun(IRichTextBoxService service) =>
        Assert.IsType<Run>(
            Assert.IsType<Paragraph>(service.Document.Blocks.FirstBlock)
                .Inlines.FirstInline);

    private static Run GetRunContaining(
        Paragraph paragraph,
        string text) =>
        Assert.Single(
            paragraph.Inlines.OfType<Run>(),
            run => run.Text.Contains(text, StringComparison.Ordinal));

    private static string GetText(IRichTextBoxService service) =>
        new TextRange(
            service.Document.ContentStart,
            service.Document.ContentEnd)
            .Text
            .TrimEnd('\r', '\n');

    private static string GetText(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;

    private static void AssertCharacterProperty(
        IRichTextBoxService service,
        int offset,
        DependencyProperty property,
        object expected)
    {
        var range = GetCharacterRange(service, offset);
        Assert.Equal(expected, range.GetPropertyValue(property));
    }

    private static TextRange GetCharacterRange(
        IRichTextBoxService service,
        int offset)
    {
        var paragraph = Assert.IsType<Paragraph>(
            service.Document.Blocks.FirstBlock);
        var position = paragraph.ContentStart;

        while(position != null &&
              position.CompareTo(paragraph.ContentEnd) < 0)
        {
            if(position.GetPointerContext(LogicalDirection.Forward) ==
               TextPointerContext.Text)
            {
                var text = position.GetTextInRun(LogicalDirection.Forward);
                if(offset < text.Length)
                {
                    var start = position.GetPositionAtOffset(
                        offset,
                        LogicalDirection.Forward)!;
                    var end = start.GetPositionAtOffset(
                        1,
                        LogicalDirection.Forward)!;
                    return new TextRange(start, end);
                }

                offset -= text.Length;
                position = position.GetPositionAtOffset(
                    text.Length,
                    LogicalDirection.Forward);
                continue;
            }

            position = position.GetNextContextPosition(
                LogicalDirection.Forward);
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

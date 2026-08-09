using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentLineSpacingPreferenceTests
{
    [Fact]
    public void Store_RestoresRatioAcrossInstances()
    {
        double original =
            Properties.Settings.Default.DocumentLineSpacingRatio;

        try
        {
            new UserDocumentLineSpacingPreferenceStore().Save(0.8);

            double restored =
                new UserDocumentLineSpacingPreferenceStore().Load();

            Assert.Equal(0.8, restored, 5);
            Assert.Equal(
                0.8,
                Properties.Settings.Default.DocumentLineSpacingRatio,
                5);
        }
        finally
        {
            Properties.Settings.Default.DocumentLineSpacingRatio = original;
            Properties.Settings.Default.Save();
        }
    }

    [WpfFact]
    public async Task DocumentLoad_AppliesStoredRatioToPlainTextAndRtf()
    {
        var preferences = new PreferenceStoreStub(0.8);
        var dispatcher = new ImmediateDispatcherService();
        var registry = new DocumentFormatHandlerRegistry(
        [
            new PlainTextDocumentFormatHandler(dispatcher),
            new RtfDocumentFormatHandler(dispatcher)
        ]);
        var loader = new FlowDocumentLoadService(
            dispatcher,
            new BookmarkServiceStub(),
            registry,
            preferences);

        using var plainStream = new MemoryStream(
            Encoding.UTF8.GetBytes("first\nsecond"));
        FlowDocument plainDocument = await loader.PrepareAsync(
            plainStream,
            new PlainTextTemplate());

        using var rtfStream = new MemoryStream(
            Encoding.ASCII.GetBytes(
                @"{\rtf1\ansi\fs40 first\par second\par}"));
        FlowDocument rtfDocument = await loader.PrepareAsync(
            rtfStream,
            new RichTextFileTemplate());

        AssertPreferredSpacing(plainDocument);
        AssertPreferredSpacing(rtfDocument);
    }

    private static void AssertPreferredSpacing(FlowDocument document)
    {
        Paragraph[] paragraphs = document.Blocks.OfType<Paragraph>().ToArray();
        Assert.NotEmpty(paragraphs);
        Assert.All(paragraphs, paragraph =>
        {
            Assert.Equal(paragraph.FontSize * 0.8, paragraph.LineHeight, 5);
            Assert.Equal(
                LineStackingStrategy.BlockLineHeight,
                paragraph.LineStackingStrategy);
        });
    }

    private sealed class PreferenceStoreStub(double ratio):
        IDocumentLineSpacingPreferenceStore
    {
        public double Load() => ratio;
        public void Save(double value) =>
            throw new NotSupportedException();
    }

    private sealed class ImmediateDispatcherService: IDispatcherService
    {
        public bool CheckAccess() => true;
        public void Invoke(Action action) => action();
        public void BeginInvoke(Action action) => action();

        public Task InvokeAsync(
            Action action,
            DispatcherPriority priority = DispatcherPriority.Background)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(
            Func<T> func,
            DispatcherPriority priority = DispatcherPriority.Background) =>
            Task.FromResult(func());
    }

    private sealed class BookmarkServiceStub: IBookmarkService
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public ObservableCollection<IBookmarkEntryViewModel> Bookmarks { get; } = [];

        public bool Exists(string name) => false;
        public void AddAtCaret(IRichTextBoxService service, string name) { }
        public bool Remove(IRichTextBoxService service, string name) => false;
        public void Rename(IRichTextBoxService service, string oldName, string newName) { }
        public bool NavigateTo(IRichTextBoxService service, string name) => false;
        public bool NavigateNext(IRichTextBoxService service) => false;
        public bool NavigatePrevious(IRichTextBoxService service) => false;
        public void InsertHyperlinkTo(
            IRichTextBoxService service,
            string bookmarkName,
            string? linkText = null) { }
        public void RebuildIndexFromDocument(IRichTextBoxService service) { }
    }
}

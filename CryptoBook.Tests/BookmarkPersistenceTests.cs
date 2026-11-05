using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Security;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Xunit;

namespace CryptoBook.Tests;

public sealed class BookmarkPersistenceTests
{
    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileRoundTrip_RestoresPositionsNamesNotesAndLinks(bool securePayload)
    {
        var (editor, bookmarks) = CreateBookmarkedEditor();
        var before = Describe(editor, bookmarks);
        string text = Text(editor.Document);
        var dispatcher = new WpfDispatcherService(Dispatcher.CurrentDispatcher);
        var registry = new DocumentFormatHandlerRegistry([new XamlPackageDocumentFormatHandler(dispatcher)]);
        var saver = new FlowDocumentSaveService(dispatcher, registry);
        string path = Path.Combine(Path.GetTempPath(), "CryptoBook-bookmarks-" + Guid.NewGuid() +
            (securePayload ? ".cbook" : ".XamlPackage"));
        try
        {
            byte[] content;
            if(securePayload)
            {
                using var output = new MemoryStream();
                await saver.SaveToStreamAsync(editor, output, new SecureFileTemplate());
                output.Position = 0;
                using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
                key.SetKey("Bkmrk!8_Kz47-WindowsTest");
                var codec = new SecureFileV2Codec(key, new SecureFileV2Options {
                    KeyDerivation = new KeyDerivationParameters(1, 8 * 1024, 1, 32)
                });
                var processor = new SecureFileProcessor(codec, new LegacySecureFileCodec(key));
                await processor.EncryptStreamAsync(output, ".XamlPackage", path);
                await using var decrypted = await processor.DecryptFileContentAsync(path);
                using var restoredBytes = new MemoryStream();
                await decrypted.Content.CopyToAsync(restoredBytes);
                content = restoredBytes.ToArray();
            }
            else
            {
                await saver.SaveToFileAsync(editor, path, new XamlPackageFileTemplate());
                content = await File.ReadAllBytesAsync(path);
            }
            var reopened = CreateEditor();
            var restored = new BookmarksService(reopened);
            var loader = new FlowDocumentLoadService(dispatcher, restored, registry,
                new Spacing(), new DocumentLineSpacing());
            using var input = new MemoryStream(content);
            await loader.LoadAsync(reopened, input, new XamlPackageFileTemplate());
            Assert.Equal(before, Describe(reopened, restored));
            Assert.Equal(text, Text(reopened.Document));
            var link = Assert.Single(new FlowDocumentWalker().Find<Hyperlink>(reopened.Document));
            Assert.Equal(bookmarks.Bookmarks[0].BookmarkUri, link.NavigateUri);
            Assert.True(restored.NavigateTo(reopened, "Раздел первый"));
            // Another save/load must not duplicate anchors or drift their positions.
            var handler = new XamlPackageDocumentFormatHandler(dispatcher);
            content = await handler.SerializeAsync(reopened.Document);
            await handler.LoadAsync(reopened.Document, content);
            restored.RebuildIndexFromDocument(reopened);
            Assert.Equal(before, Describe(reopened, restored));
        }
        finally { if(File.Exists(path)) File.Delete(path); }
    }

    [WpfFact]
    public void RenameAndNote_MarkDirty_WhileNoOpsAndRebuildDoNot()
    {
        var (editor, bookmarks) = CreateBookmarkedEditor();
        var session = new DocumentSession(editor, bookmarks: bookmarks);
        var template = new XamlPackageFileTemplate();
        session.Open("book.XamlPackage", template);
        long before = session.Revision;
        Assert.True(new BookmarkValidationService(bookmarks).CanRenameBookmark(
            editor, "Раздел первый", "РАЗДЕЛ ПЕРВЫЙ", bookmarks.Exists).Ok);
        bookmarks.Rename(editor, "Раздел первый", "РАЗДЕЛ ПЕРВЫЙ");
        Assert.True(session.IsDirty);
        Assert.Equal(before + 1, session.Revision);
        Assert.Equal("РАЗДЕЛ ПЕРВЫЙ", bookmarks.Bookmarks[0].Name);
        session.MarkSaved("book.XamlPackage", template);
        bookmarks.Bookmarks[0].Note = "Изменённая заметка";
        Assert.True(session.IsDirty);
        session.MarkSaved("book.XamlPackage", template);
        bookmarks.Rename(editor, "РАЗДЕЛ ПЕРВЫЙ", "РАЗДЕЛ ПЕРВЫЙ");
        bookmarks.Bookmarks[0].Note = "Изменённая заметка";
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.False(session.IsDirty);
        Assert.Equal("Изменённая заметка", bookmarks.Bookmarks[0].Note);
    }

    [WpfFact]
    public void PreviewSnapshot_RetainsBookmarksWithoutMutatingSource()
    {
        var (editor, bookmarks) = CreateBookmarkedEditor();
        var before = Describe(editor, bookmarks);
        var session = new DocumentSession(editor, bookmarks: bookmarks);
        session.Open("book.XamlPackage", new XamlPackageFileTemplate());
        var copy = CreateEditor();
        copy.ReplaceDocument(new DocumentPreviewService().CreatePreview(editor.Document));
        var restored = new BookmarksService(copy);
        restored.RebuildIndexFromDocument(copy);
        Assert.Equal(before, Describe(copy, restored));
        Assert.Equal(before, Describe(editor, bookmarks));
        Assert.False(session.IsDirty);
    }

    [WpfFact]
    public void FailedDocumentReplacement_RestoresPreviousBookmarkIndex()
    {
        var (editor, bookmarks) = CreateBookmarkedEditor();
        var before = Describe(editor, bookmarks);
        var session = new DocumentSession(editor, bookmarks: bookmarks);
        var template = new XamlPackageFileTemplate();
        session.Open("book.XamlPackage", template);
        Assert.Throws<ArgumentException>(() => session.Open("invalid\0path", template,
            new FlowDocument(new Paragraph(new Run("replacement")))));
        Assert.Equal(before, Describe(editor, bookmarks));
        Assert.False(session.IsDirty);
        Assert.True(bookmarks.NavigateTo(editor, "Таблица"));
    }

    [WpfFact]
    public void InactiveRecoverySnapshot_RestoresBookmarksAndCleanRevision()
    {
        var (editor, bookmarks) = CreateBookmarkedEditor();
        var before = Describe(editor, bookmarks);
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(editor, markdown, bookmarks: bookmarks);
        session.Open("book.XamlPackage", new XamlPackageFileTemplate());
        var md = new FlowDocument();
        MarkdownDocumentMetadata.SetSource(md, new MarkdownTextDocument("# Markdown", new UTF8Encoding(false), []));
        session.Open("book.md", new MarkdownFileTemplate(), md);
        var snapshot = Assert.IsType<WorkspaceDocumentSnapshot>(session.CaptureInactiveDocument());
        session.Close();
        session.RestoreInactiveDocument(snapshot);
        Assert.Equal(before, Describe(editor, bookmarks));
        Assert.False(session.IsDirty);
        bookmarks.Bookmarks[0].Note = "After restore";
        Assert.True(session.IsDirty);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutosaveRecovery_RestoresBookmarkIndexAndMetadata(bool secure)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-bookmark-recovery-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var (editor, bookmarks) = CreateBookmarkedEditor();
            var before = Describe(editor, bookmarks);
            var session = new DocumentSession(editor, bookmarks: bookmarks);
            IFileTemplate template = secure ? new SecureFileTemplate() : new XamlPackageFileTemplate();
            session.Open(Path.Combine(directory, "document" + template.DefaultExtension), template);
            session.MarkDirty();
            var dispatcher = new WpfDispatcherService(Dispatcher.CurrentDispatcher);
            var formats = new DocumentFormatHandlerRegistry([new XamlPackageDocumentFormatHandler(dispatcher)]);
            var saver = new FlowDocumentSaveService(dispatcher, formats);
            var loader = new FlowDocumentLoadService(dispatcher, bookmarks, formats,
                new Spacing(), new DocumentLineSpacing());
            using var key = new MemoryKeyProvider(new Argon2idKeyDeriver());
            key.SetKey("Bkmrk!9_Vt52-RecoveryTest");
            using var recovery = new DocumentRecoveryService(session, editor, saver, loader,
                new FileTemplateRegistry([template]), Dispatcher.CurrentDispatcher,
                Path.Combine(directory, "current.recovery"), keyProvider: key);
            await recovery.SaveSnapshotNowAsync();
            session.Close();
            Assert.Empty(bookmarks.Bookmarks);
            Assert.True(await recovery.RestoreSnapshotAsync());
            Assert.Equal(before, Describe(editor, bookmarks));
            Assert.True(session.IsDirty);
            Assert.True(bookmarks.NavigateTo(editor, "Таблица"));
            await recovery.StopAsync();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [WpfFact]
    public async Task EmptyParagraphBookmark_SurvivesRoundTrip()
    {
        var editor = CreateEditor();
        editor.Document.Blocks.Add(new Paragraph());
        editor.CaretPosition = editor.Document.ContentStart;
        var bookmarks = new BookmarksService(editor);
        bookmarks.AddAtCaret(editor, "Empty paragraph");
        var before = Describe(editor, bookmarks);
        var handler = Handler();
        byte[] content = await handler.SerializeAsync(editor.Document);
        await handler.LoadAsync(editor.Document, content);
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.Equal(before, Describe(editor, bookmarks));
        Assert.True(bookmarks.NavigateTo(editor, "Empty paragraph"));
    }

    [WpfFact]
    public async Task ImageAndFloatingText_DoNotShiftBookmarkPositions()
    {
        var editor = CreateEditor();
        var paragraph = new ParagraphService();
        paragraph.Inlines.Add(new Run("left"));
        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
        paragraph.Inlines.Add(new InlineUIContainer(new System.Windows.Controls.Image { Source = bitmap }));
        var right = new Run("right");
        paragraph.Inlines.Add(right);
        var floating = new Paragraph(new Run("floating text"));
        paragraph.Inlines.Add(new Figure(floating));
        editor.Document.Blocks.Add(paragraph);
        var bookmarks = new BookmarksService(editor);
        editor.CaretPosition = right.ContentStart;
        bookmarks.AddAtCaret(editor, "After image");
        editor.CaretPosition = floating.ContentStart;
        bookmarks.AddAtCaret(editor, "Inside figure");
        var before = Describe(editor, bookmarks);
        string text = Text(editor.Document);
        var handler = Handler();
        byte[] content = await handler.SerializeAsync(editor.Document);
        await handler.LoadAsync(editor.Document, content);
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.Equal(before, Describe(editor, bookmarks));
        Assert.Equal(text, Text(editor.Document));
    }

    [WpfFact]
    public async Task NestedAndAdjacentAnchors_SurviveTextRangeInlineNormalization()
    {
        var editor = CreateEditor();
        editor.Document.Blocks.Add(new Paragraph(new Run("ab")));
        editor.CaretPosition = editor.Document.ContentStart;
        var bookmarks = new BookmarksService(editor);
        bookmarks.AddAtCaret(editor, "Outer");
        bookmarks.NavigateTo(editor, "Outer");
        bookmarks.AddAtCaret(editor, "Inner");
        bookmarks.AddAtCaret(editor, "Adjacent");
        var before = Describe(editor, bookmarks);
        var handler = Handler();
        byte[] content = await handler.SerializeAsync(editor.Document);
        await handler.LoadAsync(editor.Document, content);
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.Equal(before.OrderBy(b => b.Name), Describe(editor, bookmarks).OrderBy(b => b.Name));
        foreach(var bookmark in bookmarks.Bookmarks)
            Assert.True(bookmarks.NavigateTo(editor, bookmark.Name));
    }

    [WpfTheory]
    [InlineData("missing")]
    [InlineData("invalid-json")]
    [InlineData("unknown-version")]
    [InlineData("wrong-digest")]
    public async Task MissingOrInvalidMetadata_DoesNotPreventLoadingText(string mode)
    {
        var (editor, _) = CreateBookmarkedEditor();
        string text = Text(editor.Document);
        var handler = Handler();
        byte[] content = await handler.SerializeAsync(editor.Document);
        using var package = new MemoryStream();
        package.Write(content);
        using(var archive = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(XamlPackageBookmarkCodec.EntryPath)!;
            string json;
            using(var reader = new StreamReader(entry.Open())) json = reader.ReadToEnd();
            entry.Delete();
            if(mode != "missing")
            {
                json = mode switch {
                    "invalid-json" => "{",
                    "unknown-version" => json.Replace("\"version\":1", "\"version\":999"),
                    _ => json.Replace("\"textDigest\":\"", "\"textDigest\":\"wrong")
                };
                using var writer = new StreamWriter(archive.CreateEntry(XamlPackageBookmarkCodec.EntryPath).Open());
                writer.Write(json);
            }
        }
        var reopened = CreateEditor();
        await handler.LoadAsync(reopened.Document, package.ToArray());
        var restored = new BookmarksService(reopened);
        restored.RebuildIndexFromDocument(reopened);
        Assert.Equal(text, Text(reopened.Document));
        Assert.Empty(restored.Bookmarks);
    }

    [WpfTheory]
    [InlineData("invalid-range")]
    [InlineData("invalid-name")]
    [InlineData("null-record")]
    public async Task InvalidBookmarkRecord_IsIgnoredWithoutLosingTextOrOtherBookmarks(string mode)
    {
        var (editor, _) = CreateBookmarkedEditor();
        string text = Text(editor.Document);
        var handler = Handler();
        using var package = new MemoryStream();
        package.Write(await handler.SerializeAsync(editor.Document));
        using(var archive = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(XamlPackageBookmarkCodec.EntryPath)!;
            JsonNode metadata;
            using(var stream = entry.Open()) metadata = JsonNode.Parse(stream)!;
            var records = metadata["bookmarks"]!.AsArray();
            switch(mode)
            {
                case "invalid-range": records[0]!["length"] = int.MaxValue; break;
                case "invalid-name": records[0]!["anchorId"] = "invalid name"; break;
                default: records[0] = null; break;
            }
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(XamlPackageBookmarkCodec.EntryPath).Open());
            writer.Write(metadata.ToJsonString());
        }
        var reopened = CreateEditor();
        await handler.LoadAsync(reopened.Document, package.ToArray());
        var bookmarks = new BookmarksService(reopened);
        bookmarks.RebuildIndexFromDocument(reopened);
        Assert.Equal(text, Text(reopened.Document));
        Assert.Equal(2, bookmarks.Bookmarks.Count);
        Assert.DoesNotContain(bookmarks.Bookmarks, b => b.Name == "Раздел первый");
    }

    [WpfFact]
    public async Task LegacyNamedAnchor_IsPreservedAndFileWithoutBookmarksStillLoads()
    {
        var editor = CreateEditor();
        editor.Document.Blocks.Add(new Paragraph(new Span(new Run("legacy")) { Name = "LegacyName" }));
        var handler = Handler();
        byte[] content = await handler.SerializeAsync(editor.Document);
        await handler.LoadAsync(editor.Document, content);
        var bookmarks = new BookmarksService(editor);
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.Equal("LegacyName", Assert.Single(bookmarks.Bookmarks).Name);
        Assert.True(bookmarks.NavigateTo(editor, "LegacyName"));
        var oldFile = await new XamlPackageFileTemplate().GetInitialContentAsync(default);
        await handler.LoadAsync(editor.Document, oldFile);
        bookmarks.RebuildIndexFromDocument(editor);
        Assert.Empty(bookmarks.Bookmarks);
    }

    private static (IRichTextBoxService Editor, BookmarksService Bookmarks) CreateBookmarkedEditor()
    {
        var editor = CreateEditor();
        var first = new Paragraph(new Run("До 😀 "));
        first.Inlines.Add(new Bold(new Run("оформленный текст")));
        first.Inlines.Add(new LineBreak());
        first.Inlines.Add(new Run("после"));
        editor.Document.Blocks.Add(first);
        editor.Document.Blocks.Add(new Paragraph());
        var cellParagraph = new Paragraph(new Run("В таблице"));
        var table = new Table();
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell(cellParagraph));
        group.Rows.Add(row); table.RowGroups.Add(group); editor.Document.Blocks.Add(table);
        var listParagraph = new Paragraph(new Run("Элемент списка"));
        editor.Document.Blocks.Add(new List(new ListItem(listParagraph)));
        var bookmarks = new BookmarksService(editor);
        editor.CaretPosition = first.Inlines.OfType<Run>().First().ContentStart.GetPositionAtOffset(6)!;
        bookmarks.AddAtCaret(editor, "Раздел первый");
        bookmarks.Bookmarks[0].Note = "Заметка с \"кавычками\"\nи новой строкой";
        editor.CaretPosition = cellParagraph.ContentEnd;
        bookmarks.AddAtCaret(editor, "Таблица");
        editor.CaretPosition = listParagraph.ContentStart;
        bookmarks.AddAtCaret(editor, "Список");
        editor.CaretPosition = listParagraph.ContentEnd;
        editor.ClearSelection();
        bookmarks.InsertHyperlinkTo(editor, "Раздел первый", "К разделу");
        return (editor, bookmarks);
    }

    private static (string Name, string Note, string? Uri, string Prefix)[] Describe(
        IRichTextBoxService editor, BookmarksService bookmarks) =>
        bookmarks.Bookmarks.Select(b => {
            var anchor = new FlowDocumentWalker().Find<Span>(editor.Document)
                .Single(s => s.Name == b.BookmarkUri!.OriginalString.TrimStart('#'));
            return (b.Name, b.Note, b.BookmarkUri?.OriginalString,
                new TextRange(editor.Document.ContentStart, anchor.ContentStart).Text);
        }).ToArray();

    private static string Text(FlowDocument document) => new TextRange(document.ContentStart, document.ContentEnd).Text;
    private static XamlPackageDocumentFormatHandler Handler() =>
        new(new WpfDispatcherService(Dispatcher.CurrentDispatcher));
    private static IRichTextBoxService CreateEditor()
    {
        IRichTextBoxService editor = new RichTextBoxService(new ParagraphFactory(),
            new TestUriNavigationService(), new DocumentAppearanceDefaults());
        editor.Document.Blocks.Clear();
        return editor;
    }
    private sealed class ParagraphFactory : IParagraphFactory
    {
        public IParagraphService Create(Inline? inline = null)
        {
            var paragraph = new ParagraphService();
            if(inline is not null) paragraph.Inlines.Add(inline);
            return paragraph;
        }
    }
    private sealed class Spacing : IDocumentLineSpacingPreferenceStore
    {
        public double Load() => 1.2;
        public void Save(double ratio) { }
    }
}

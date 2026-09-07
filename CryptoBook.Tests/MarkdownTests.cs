using Autofac;

using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.IO;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class MarkdownTests
{
    [WpfFact]
    public async Task OpeningMarkdown_ActivatesExactSource_AndTracksTextChanges()
    {
        const string source = "# Heading\r\n\r\nline  \r\nnext";
        var state = new MarkdownDocumentState();
        var template = new MarkdownFileTemplate();
        var handler = new MarkdownDocumentFormatHandler(
            new WpfDispatcherService(Dispatcher.CurrentDispatcher));
        var document = new FlowDocument();
        await handler.LoadAsync(
            document,
            Encoding.UTF8.GetBytes(source));
        IRichTextBoxService editor = CreateEditor();
        var session = new DocumentSession(editor, state);

        session.Open(
            Path.Combine(Path.GetTempPath(), "book.md"),
            template,
            document);

        Assert.True(state.IsActive);
        Assert.Equal(source, state.Text);
        Assert.False(session.IsDirty);

        state.Text += "\r\nchanged";

        Assert.True(session.IsDirty);
    }

    [WpfFact]
    public async Task MarkdownSave_UsesSourceText_NotEditorOrPreviewDocument()
    {
        const string source = "# Source\n\n**bold**";
        var state = new MarkdownDocumentState();
        state.Activate(
            new MarkdownTextDocument(
                source,
                new UTF8Encoding(false),
                []),
            Path.Combine(Path.GetTempPath(), "book.md"));
        var template = new MarkdownFileTemplate();
        var dispatcher = new WpfDispatcherService(
            Dispatcher.CurrentDispatcher);
        var handler = new MarkdownDocumentFormatHandler(dispatcher);
        var service = new FlowDocumentSaveService(
            dispatcher,
            new DocumentFormatHandlerRegistry([handler]),
            state);
        IRichTextBoxService editor = CreateEditor();
        editor.Document.Blocks.Clear();
        editor.Document.Blocks.Add(new Paragraph(new Run("wrong editor")));
        FlowDocument preview = new MarkdownFlowDocumentRenderer().Render(
            source,
            Path.Combine(Path.GetTempPath(), "book.md"));
        ((Run)((Paragraph)preview.Blocks.FirstBlock!).Inlines.FirstInline!).Text =
            "wrong preview";
        await using var output = new MemoryStream();

        await service.SaveToStreamAsync(editor, output, template);

        Assert.Equal(source, Encoding.UTF8.GetString(output.ToArray()));
    }

    [WpfFact]
    public void Renderer_SupportsRequiredBlocksAndInlineFormatting()
    {
        const string markdown = """
            # Heading

            **bold** *italic* ~~strike~~ [safe](https://example.com) [unsafe](file:///c:/secret.txt)

            > quote

            - item

            ---

            ```csharp
            var value = 1;
            ```

            | A | B |
            |---|---|
            | 1 | 2 |

            <script>alert('never')</script>
            """;

        FlowDocument document = new MarkdownFlowDocumentRenderer().Render(
            markdown,
            Path.Combine(Path.GetTempPath(), "book.md"));
        Paragraph body = document.Blocks
            .OfType<Paragraph>()
            .First(paragraph => new TextRange(
                paragraph.ContentStart,
                paragraph.ContentEnd).Text.Contains("bold"));

        Assert.Contains(document.Blocks, block => block is Section);
        Assert.Contains(document.Blocks, block => block is List);
        Assert.Contains(document.Blocks, block => block is Table);
        Assert.Contains(
            document.Blocks.OfType<Paragraph>(),
            paragraph => paragraph.FontFamily?.Source.Contains(
                "Mono",
                StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(
            body.Inlines.OfType<Span>(),
            span => span.FontWeight == FontWeights.Bold);
        Assert.Contains(
            body.Inlines.OfType<Span>(),
            span => span.FontStyle == FontStyles.Italic);
        Assert.Contains(
            body.Inlines.OfType<Span>(),
            span => span.TextDecorations == TextDecorations.Strikethrough);
        Hyperlink hyperlink = Assert.Single(body.Inlines.OfType<Hyperlink>());
        Assert.Equal(Uri.UriSchemeHttps, hyperlink.NavigateUri.Scheme);
        string renderedText = new TextRange(
            document.ContentStart,
            document.ContentEnd).Text;
        Assert.Contains("script", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [WpfFact]
    public async Task Renderer_LoadsOnlyRelativeLocalImages()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string imagePath = Path.Combine(directory, "pixel.png");
            await File.WriteAllBytesAsync(
                imagePath,
                await new ImageFileTemplate().GetInitialContentAsync(
                    CancellationToken.None));
            var renderer = new MarkdownFlowDocumentRenderer();

            FlowDocument local = renderer.Render(
                "![local](pixel.png)",
                Path.Combine(directory, "book.md"));
            FlowDocument remote = renderer.Render(
                "![remote](https://example.com/pixel.png)",
                Path.Combine(directory, "book.md"));

            Assert.IsType<Image>(Assert.IsType<InlineUIContainer>(
                ((Paragraph)local.Blocks.FirstBlock!).Inlines.FirstInline!).Child);
            Assert.Empty(
                ((Paragraph)remote.Blocks.FirstBlock!).Inlines
                    .OfType<InlineUIContainer>());
        }
        finally
        {
            if(Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [WpfFact]
    public void ModeSwitch_RebuildsPreview_AndReturnsOriginalMarkdown()
    {
        const string source = "# Original\n\n**source text**";
        var state = new MarkdownDocumentState();
        string path = Path.Combine(Path.GetTempPath(), "book.md");
        state.Activate(
            new MarkdownTextDocument(
                source,
                new UTF8Encoding(false),
                []),
            path);
        var session = new DocumentSession(CreateEditor(), state);
        session.Open(path, new MarkdownFileTemplate());
        var viewModel = new MarkdownEditorViewModel(
            state,
            new MarkdownFlowDocumentRenderer(),
            new TestUriNavigationService(),
            new StubMenuFileViewModel(),
            session);

        viewModel.ToggleView.Execute(null);
        FlowDocument firstPreview = viewModel.PreviewDocument!;
        ((Run)((Paragraph)firstPreview.Blocks.FirstBlock!)
            .Inlines.FirstInline!).Text = "mutated preview";

        viewModel.ToggleView.Execute(null);

        Assert.False(viewModel.IsPreviewMode);
        Assert.Null(viewModel.PreviewDocument);
        Assert.Equal(source, viewModel.MarkdownText);

        viewModel.MarkdownText += "\n\nnew text";
        viewModel.ToggleView.Execute(null);

        Assert.NotSame(firstPreview, viewModel.PreviewDocument);
        Assert.Contains(
            "new text",
            new TextRange(
                viewModel.PreviewDocument!.ContentStart,
                viewModel.PreviewDocument.ContentEnd).Text);
    }

    [WpfFact]
    public async Task LockSnapshot_PreservesCurrentMarkdownSource()
    {
        const string source = "# Unsaved\n\nlatest **Markdown**";
        string directory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var state = new MarkdownDocumentState();
            state.Activate(
                new MarkdownTextDocument(
                    source,
                    new UTF8Encoding(false),
                    []),
                Path.Combine(directory, "book.md"));
            var template = new MarkdownFileTemplate();
            var dispatcher = new WpfDispatcherService(
                Dispatcher.CurrentDispatcher);
            var handler = new MarkdownDocumentFormatHandler(dispatcher);
            var registry = new FileTemplateRegistry([template]);
            var saveService = new FlowDocumentSaveService(
                dispatcher,
                new DocumentFormatHandlerRegistry([handler]),
                state);
            var snapshot = new LockSnapshotService(
                new PassthroughSecureFileProcessor(),
                saveService,
                new MarkdownLoadService(handler),
                Path.Combine(directory, "last.lock.cbook"),
                registry,
                state);
            IRichTextBoxService editor = CreateEditor();
            editor.Document.Blocks.Clear();
            editor.Document.Blocks.Add(
                new Paragraph(new Run("stale FlowDocument")));
            var metadata = new LockSnapshotMetadata(
                Path.Combine(directory, "book.md"),
                "book.md",
                template.Id,
                true,
                DateTimeOffset.UtcNow)
            {
                InactiveDocument = new WorkspaceDocumentSnapshot(
                    "home.txt", "home.txt", "Text", 3, 1, false,
                    Encoding.UTF8.GetBytes("independent home content"))
            };

            await snapshot.CreateAndVerifyAsync(editor, metadata);
            (FlowDocument restored, LockSnapshotMetadata restoredMetadata) =
                await snapshot.ReadAndVerifyAsync();

            Assert.Equal("Markdown", restoredMetadata.ContentTemplateId);
            Assert.Equal(metadata.InactiveDocument.Content, restoredMetadata.InactiveDocument!.Content);
            Assert.Equal(3, restoredMetadata.InactiveDocument.Revision);
            Assert.Equal(
                source,
                MarkdownDocumentMetadata.GetSource(restored)!.Text);
        }
        finally
        {
            if(Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MarkdownEditor_IsRegisteredAsSeparatePage()
    {
        var registry = new CryptoBook.Injections.PageRegistry();

        Assert.Equal(
            typeof(CryptoBook.MyPages.MarkdownEditor),
            registry.Resolve("MarkdownEditor"));
    }

    [WpfFact]
    public void SideMenu_AllowsHomeAndMarkdownNavigationWhileMarkdownIsOpen()
    {
        var app = Application.Current ?? new Application();
        using Autofac.IContainer container =
            new Startup().ConfigureServices(app);
        var navigation = new NavigationServiceStub("MarkdownEditor");
        using ILifetimeScope scope = container.BeginLifetimeScope(builder =>
            builder.RegisterInstance(navigation)
                .As<IPageNavigationService>()
                .SingleInstance());
        IMarkdownDocumentState state =
            scope.Resolve<IMarkdownDocumentState>();
        IDocumentSession session = scope.Resolve<IDocumentSession>();
        session.Open(Path.Combine(Path.GetTempPath(), "home.txt"),
            new PlainTextTemplate(), new FlowDocument(new Paragraph(new Run("Home content"))));
        var markdownDocument = new FlowDocument();
        MarkdownDocumentMetadata.SetSource(markdownDocument,
            new MarkdownTextDocument(
                "# Open document",
                new UTF8Encoding(false),
                []));
        session.Open(Path.Combine(Path.GetTempPath(), "book.md"),
            new MarkdownFileTemplate(), markdownDocument);
        var frame = new CryptoBook.Models.MyFrameModel(navigation, state, session);
        ISideMenuViewModel sideMenu = scope.Resolve<ISideMenuViewModel>();
        IHomeViewModel homeView = scope.Resolve<IHomeViewModel>();

        var home = sideMenu.MenuItems[0].Children[0];
        var markdown = sideMenu.MenuItems[0].Children[1];

        Assert.True(home.Command!.CanExecute(null));
        home.Command.Execute(null);
        Assert.Equal("Home", navigation.CurrentKey);
        Assert.False(state.IsActive);
        Assert.True(homeView.HasDocument);

        Assert.True(markdown.Command!.CanExecute(null));
        markdown.Command.Execute(null);
        Assert.Equal("MarkdownEditor", navigation.CurrentKey);
        Assert.True(state.IsActive);
        Assert.Equal("# Open document", state.Text);

        ((IWorkspaceDocumentSession)session).CloseCurrent();
        Assert.False(markdown.Command.CanExecute(null));
    }

    private static IRichTextBoxService CreateEditor() =>
        new RichTextBoxService(
            new TestParagraphFactory(),
            new TestUriNavigationService(),
            new DocumentAppearanceDefaults());

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

    private sealed class StubMenuFileViewModel: IMenuFileViewModel
    {
        private static ICommand NoOperation { get; } =
            new RelayCommand(_ => { });

        public ICommand NewFile => NoOperation;
        public ICommand OpenFile => NoOperation;
        public ICommand SaveFile => NoOperation;
        public ICommand SaveAsFile => NoOperation;
        public ICommand PrintFile => NoOperation;
        public ICommand FileOverview => NoOperation;
        public ICommand OpenDirectory => NoOperation;
        public ICommand UpdateFile => NoOperation;
        public ICommand CloseFile => NoOperation;
        public ICommand WorkingDirectorySynchronization => NoOperation;
    }

    private sealed class NavigationServiceStub(string initialKey):
        IPageNavigationService
    {
        private string currentKey = initialKey;

        public event PropertyChangedEventHandler? PropertyChanged;
        public Page? CurrentPage => null;
        public string? CurrentKey => currentKey;
        public bool CanGoBack => false;
        public bool CanGoForward => false;
        public IReadOnlyList<string>? Keys => [currentKey];

        public void Navigate(string key, object? args = null)
        {
            currentKey = key;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(CurrentKey)));
        }

        public void GoBack()
        {
        }

        public void GoForward()
        {
        }

        public void Remove(string key)
        {
        }
    }

    private sealed class MarkdownLoadService: IFlowDocumentLoadService
    {
        private readonly MarkdownDocumentFormatHandler handler;

        public MarkdownLoadService(MarkdownDocumentFormatHandler handler)
        {
            this.handler = handler;
        }

        public async Task<FlowDocument> PrepareAsync(
            Stream source,
            IFileTemplate template,
            CancellationToken cancellationToken = default,
            IProgressReporter? progress = null)
        {
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            var document = new FlowDocument();
            await handler.LoadAsync(
                document,
                buffer.ToArray(),
                cancellationToken);
            return document;
        }

        public async Task LoadAsync(
            IRichTextBoxService richTextBoxService,
            Stream source,
            IFileTemplate template,
            CancellationToken cancellationToken = default,
            IProgressReporter? progress = null)
        {
            richTextBoxService.ReplaceDocument(await PrepareAsync(
                source,
                template,
                cancellationToken,
                progress));
        }
    }

    private sealed class PassthroughSecureFileProcessor:
        ISecureFileProcessor
    {
        public async Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            await using FileStream output = new(
                outputFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);
            await input.CopyToAsync(output, cancellationToken);
        }

        public Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DecryptedFileContent(
                new FileStream(
                    inputFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    useAsync: true),
                ".cbook"));

        public Task EncryptFileAsync(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

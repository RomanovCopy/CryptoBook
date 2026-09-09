using Autofac;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace CryptoBook.Tests;

public sealed class WorkspaceDocumentSessionTests
{
    [WpfFact]
    public void SavingDocument_HoldsItsPageUntilOperationCompletes()
    {
        var (_, _, session) = CreateSession();
        OpenHome(session);
        OpenMarkdown(session);
        using(session.HoldActiveDocument())
        {
            Assert.False(session.SelectPage("Home"));
            session.MarkSaved("saved.md", new MarkdownFileTemplate());
            Assert.Equal("MarkdownEditor", session.ActivePageKey);
        }
        Assert.True(session.SelectPage("Home"));
        Assert.Equal(Path.GetFullPath("home.txt"), session.FilePath);
        session.SelectPage("MarkdownEditor");
        Assert.Equal(Path.GetFullPath("saved.md"), session.FilePath);
    }

    [WpfTheory]
    [InlineData(UnsavedChangesChoice.Save)]
    [InlineData(UnsavedChangesChoice.Discard)]
    [InlineData(UnsavedChangesChoice.Cancel)]
    public async Task ClosingWindow_ChecksHiddenChangesAndRestoresSelectedPage(UnsavedChangesChoice choice)
    {
        var (_, markdown, session) = CreateSession();
        OpenHome(session);
        OpenMarkdown(session);
        markdown.Text = "# Unsaved";
        session.SelectPage("Home");
        var dialogs = new Dialogs(choice);
        var saver = new Saver(session);
        var guard = new UnsavedChangesGuard(session, saver, dialogs);
        Assert.Equal(choice != UnsavedChangesChoice.Cancel, await guard.CanCloseAsync());
        Assert.Equal(1, dialogs.Prompts);
        Assert.Equal("Home", session.ActivePageKey);
        Assert.Equal(choice == UnsavedChangesChoice.Save ? Path.GetFullPath("book.md") : null, saver.SavedPath);
        session.SelectPage("MarkdownEditor");
        Assert.Equal("# Unsaved", markdown.Text);
        Assert.Equal(choice != UnsavedChangesChoice.Save, session.IsDirty);
    }

    private sealed class Saver(DocumentSession session) : ICurrentDocumentSaver
    {
        public string? SavedPath { get; private set; }
        public Task<bool> TrySaveCurrentAsync(CancellationToken cancellationToken = default)
        {
            SavedPath = session.FilePath;
            session.MarkSaved(session.FilePath!, session.Template!);
            return Task.FromResult(true);
        }
    }

    private sealed class Dialogs(UnsavedChangesChoice choice) : IDocumentDialogService
    {
        public int Prompts { get; private set; }
        public bool ConfirmRecovery() => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() { Prompts++; return choice; }
        public UnsavedChangesChoice ConfirmSwitchWithUnsavedChanges() => choice;
        public void ShowRecoveryError(Exception exception) { }
        public void ShowRecoveryCleanupError(Exception exception) { }
    }

    [WpfFact]
    public void Navigation_PreservesBothContentsPathsAndDirtyRevisions()
    {
        var (editor, markdown, session) = CreateSession();
        using var container = CreateContainer();
        using var navigation = new PageNavigationService(container);
        var frame = new MyFrameModel(navigation, markdown, session);
        Assert.Equal("Home", navigation.CurrentKey);
        Assert.False(session.HasDocument);

        OpenHome(session);
        FlowDocument homeDocument = editor.Document;
        session.MarkDirty();
        long homeRevision = session.Revision;
        OpenMarkdown(session);
        markdown.Text = "# Changed markdown";
        long markdownRevision = session.Revision;
        Assert.Equal(new[] { "Home", "MarkdownEditor" }, navigation.Keys);

        navigation.GoBack();
        Assert.Equal("Home", navigation.CurrentKey);
        Assert.Same(homeDocument, editor.Document);
        Assert.Contains("Home content", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
        Assert.Equal(Path.GetFullPath("home.txt"), session.FilePath);
        Assert.Equal(homeRevision, session.Revision);
        Assert.True(session.IsDirty);
        Assert.False(editor.IsReadOnly);

        navigation.GoForward();
        Assert.Equal("MarkdownEditor", navigation.CurrentKey);
        Assert.Equal("# Changed markdown", markdown.Text);
        Assert.Equal(Path.GetFullPath("book.md"), session.FilePath);
        Assert.Equal(markdownRevision, session.Revision);
        Assert.True(session.IsDirty);

        navigation.Navigate("Home");
        session.MarkSaved("home.txt", new PlainTextTemplate());
        navigation.Navigate("MarkdownEditor");
        Assert.True(session.IsDirty);
        Assert.Equal("# Changed markdown", markdown.Text);
    }

    [WpfFact]
    public void MarkdownOnly_RemovesStartPage_AndLastCloseRestoresIt()
    {
        var (_, markdown, session) = CreateSession();
        using var container = CreateContainer();
        using var navigation = new PageNavigationService(container);
        var frame = new MyFrameModel(navigation, markdown, session);
        OpenMarkdown(session);
        Assert.Equal(new[] { "MarkdownEditor" }, navigation.Keys);
        Assert.False(navigation.CanGoBack);
        navigation.Navigate("Home");
        Assert.Equal("MarkdownEditor", navigation.CurrentKey);
        Assert.Equal(new[] { "MarkdownEditor" }, navigation.Keys);
        Assert.False(navigation.CanGoForward);
        session.CloseCurrent();
        Assert.Equal("Home", navigation.CurrentKey);
        Assert.Equal(new[] { "Home" }, navigation.Keys);
        Assert.False(session.HasDocument);
    }

    [WpfTheory]
    [InlineData("Home", "MarkdownEditor")]
    [InlineData("MarkdownEditor", "Home")]
    public void CloseCurrent_ShowsRemainingDocument(string closing, string remaining)
    {
        var (_, markdown, session) = CreateSession();
        using var container = CreateContainer();
        using var navigation = new PageNavigationService(container);
        var frame = new MyFrameModel(navigation, markdown, session);
        OpenHome(session);
        OpenMarkdown(session);
        navigation.Navigate(closing);
        session.CloseCurrent();
        Assert.Equal(remaining, navigation.CurrentKey);
        Assert.True(session.HasDocument);
        Assert.Equal(remaining == "Home", session.HasHomeDocument);
        Assert.Equal(remaining == "MarkdownEditor", session.HasMarkdownDocument);
    }

    [WpfFact]
    public void ReplacingOneFormat_LeavesOtherDocumentIntact()
    {
        var (_, markdown, session) = CreateSession();
        OpenHome(session);
        OpenMarkdown(session);
        markdown.Text = "# Unsaved";
        session.Open("second.txt", new PlainTextTemplate(),
            new FlowDocument(new Paragraph(new Run("Second home"))));
        Assert.True(session.SelectPage("MarkdownEditor"));
        Assert.Equal("# Unsaved", markdown.Text);
        Assert.True(session.IsDirty);
        session.Close();
        Assert.False(session.HasHomeDocument);
        Assert.False(session.HasMarkdownDocument);
        Assert.False(session.SelectPage("MarkdownEditor"));
    }

    [WpfTheory]
    [InlineData("Home")]
    [InlineData("MarkdownEditor")]
    public async Task WorkspaceSnapshot_RestoresInactiveContentAndMetadata(string activePage)
    {
        var (_, markdown, session) = CreateSession();
        OpenHome(session);
        session.MarkDirty();
        OpenMarkdown(session);
        markdown.Text = "# Unsaved\r\n" + new string('x', 1100000);
        session.SelectPage(activePage);
        WorkspaceDocumentSnapshot snapshot = session.CaptureInactiveDocument()!;
        using var envelope = new MemoryStream();
        // Non-zero envelope offsets must not break ZIP offsets.
        envelope.Write(new byte[37]);
        using var active = new MemoryStream(Encoding.UTF8.GetBytes("active"));
        await WorkspaceSnapshotPayload.WriteAsync(envelope, active, snapshot);
        envelope.Position = 37;
        var payload = await WorkspaceSnapshotPayload.ReadAsync(envelope);
        using var restoredActive = payload.Active;
        Assert.Equal("active", Encoding.UTF8.GetString(restoredActive.ToArray()));

        session.Close();
        session.RestoreInactiveDocument(payload.Inactive);
        Assert.True(session.SelectPage(activePage == "Home" ? "MarkdownEditor" : "Home"));
        Assert.Equal(snapshot.FilePath, session.FilePath);
        Assert.Equal(snapshot.TemplateId, session.Template?.Id);
        Assert.Equal(snapshot.Revision, session.Revision);
        Assert.Equal(snapshot.SavedRevision, session.SavedRevision);
        Assert.True(session.IsDirty);
        if(activePage == "Home")
            Assert.EndsWith(new string('x', 1100000), markdown.Text);
    }

    private static (IRichTextBoxService, MarkdownDocumentState, DocumentSession) CreateSession()
    {
        var editor = new RichTextBoxService(new ParagraphFactory(),
            new TestUriNavigationService(), new DocumentAppearanceDefaults());
        var markdown = new MarkdownDocumentState();
        return (editor, markdown, new DocumentSession(editor, markdown));
    }

    [WpfFact]
    public void InactiveSnapshot_RestoresSelectedPageWidth()
    {
        var (editor, _, session) = CreateSession();
        OpenHome(session);
        DocumentPageLayout.Apply(editor.Document, CryptoBook.DTO.DocumentPaperSize.A3, true);
        double width = editor.Document.PageWidth;
        OpenMarkdown(session);
        var snapshot = session.CaptureInactiveDocument();
        session.Close();
        session.RestoreInactiveDocument(snapshot);
        Assert.Equal(width, editor.Document.PageWidth);
        Assert.True(double.IsNaN(editor.Document.PageHeight));
    }

    private static void OpenHome(DocumentSession session) => session.Open("home.txt",
        new PlainTextTemplate(), new FlowDocument(new Paragraph(new Run("Home content"))));

    private static void OpenMarkdown(DocumentSession session)
    {
        var document = new FlowDocument();
        MarkdownDocumentMetadata.SetSource(document,
            new MarkdownTextDocument("# Original", new UTF8Encoding(false), []));
        session.Open("book.md", new MarkdownFileTemplate(), document);
    }

    private static IContainer CreateContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<Page>();
        builder.RegisterInstance(new Registry()).As<IPageRegistry>();
        return builder.Build();
    }

    private sealed class Registry : IPageRegistry
    {
        public Type Resolve(string key) => typeof(Page);
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
}

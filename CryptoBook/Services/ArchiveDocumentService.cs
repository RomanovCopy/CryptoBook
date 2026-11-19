using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows.Documents;

namespace CryptoBook.Services;

/// <summary>Internal document editing; no external editor or untracked plaintext copy is launched.</summary>
public sealed class ArchiveDocumentService: IService, IDisposable
{
    private readonly ArchiveStorageProvider archives;
    private readonly IFlowDocumentLoadService loader;
    private readonly IFileTemplateRegistry templates;
    private readonly IDocumentSession documents;
    private readonly IRichTextBoxService editor;
    private readonly IUnsavedChangesGuard guard;
    private readonly IBookmarkService bookmarks;
    private readonly ISecureFileProcessor secureFiles;
    private readonly IEncryptionKeyRequestService keys;
    private readonly IProgressDialogService progressDialogs;
    private readonly Guid owner = Guid.NewGuid();
    private readonly Dictionary<string, string> versions = new(StringComparer.Ordinal);

    public ArchiveDocumentService(ArchiveStorageProvider archives, IFlowDocumentLoadService loader,
        IFileTemplateRegistry templates, IDocumentSession documents, IRichTextBoxService editor,
        IUnsavedChangesGuard guard, IBookmarkService bookmarks, ISecureFileProcessor secureFiles,
        IEncryptionKeyRequestService keys, IProgressDialogService progressDialogs)
    {
        this.archives = archives; this.loader = loader; this.templates = templates; this.documents = documents;
        this.editor = editor; this.guard = guard; this.bookmarks = bookmarks; this.secureFiles = secureFiles; this.keys = keys;
        this.progressDialogs = progressDialogs;
        documents.PropertyChanged += OnDocumentChanged;
    }

    internal async Task<WorkspaceFileOpenResult> OpenAsync(StorageLocation source, CancellationToken token)
    {
        var metadata = await archives.GetMetadataAsync(source, token) ?? throw new FileNotFoundException();
        if(metadata.Size > 64L * 1024 * 1024) throw new IOException(ArchivePathPolicy.Text("DocumentLimit"));
        var template = templates.GetAll().FirstOrDefault(item => item.CanHandleExtension(Path.GetExtension(metadata.Name)));
        if(template?.OpenMode != FileOpenMode.Document)
            return WorkspaceFileOpenResult.Fail(ArchivePathPolicy.Text("InternalDocumentsOnly"));
        if(template is SecureFileTemplate && !keys.EnsureKeyAvailable()) return WorkspaceFileOpenResult.Cancel();
        FlowDocument prepared = await progressDialogs.RunAsync(ArchivePathPolicy.Text("Open"), async (progress, dialogToken) =>
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, dialogToken);
            return await PrepareAsync(source, template, progress, cancellation.Token);
        });
        string originalPage = (documents as IWorkspaceDocumentSession)?.ActivePageKey ?? "Home";
        if(documents is IWorkspaceDocumentSession workspace)
            workspace.SelectPage(MarkdownDocumentMetadata.GetSource(prepared) is not null ? "MarkdownEditor" : "Home");
        try { if(!await guard.CanProceedAsync(token)) return WorkspaceFileOpenResult.Cancel(); }
        finally { (documents as IWorkspaceDocumentSession)?.SelectPage(originalPage); }
        // Own a separate session so closing FileExplorer does not invalidate Save.
        StorageLocation retained = await archives.RetainDocumentAsync(owner, source, token);
        string path = retained.ToString();
        versions[path] = await archives.GetSourceHashAsync(retained, token);
        documents.Open(path, template, prepared);
        documents.SetDisplayName(metadata.Name);
        bookmarks.RebuildIndexFromDocument(editor);
        return WorkspaceFileOpenResult.InternalSuccess();
    }

    private async Task<FlowDocument> PrepareAsync(StorageLocation source, IFileTemplate template,
        IProgressReporter progress, CancellationToken token)
    {
        if(template is SecureFileTemplate)
        {
            string temporary = CreateWorkspace();
            try
            {
                string encrypted = Path.Combine(temporary, "protected.cbook");
                var copy = await archives.PullToLocalAsync(source, new("local", encrypted), progress, token);
                if(!copy.Success) throw new IOException(copy.ErrorMessage);
                await using var decrypted = await secureFiles.DecryptFileContentAsync(encrypted, progress, token);
                var contentTemplate = templates.GetAll().FirstOrDefault(item => item.CanHandleExtension(decrypted.OriginalExtension));
                if(contentTemplate?.OpenMode != FileOpenMode.Document)
                    throw new NotSupportedException(ArchivePathPolicy.Text("InternalDocumentsOnly"));
                return await loader.PrepareAsync(decrypted.Content, contentTemplate, token, progress);
            }
            finally { await ArchiveService.RemoveWorkspaceAsync(temporary); }
        }
        else
        {
            await using var input = await archives.OpenRawReadAsync(source, token);
            return await loader.PrepareAsync(input, template, token, progress);
        }
    }

    internal async Task SaveAsync(string path, IFileTemplate template, IFlowDocumentSaveService saver,
        IRichTextBoxService richTextBox, IMarkdownDocumentState? markdown, FlowDocument? snapshot,
        IProgressReporter progress, CancellationToken token)
    {
        var location = StorageLocation.Parse(path);
        if(!archives.CanEdit(location) || !versions.TryGetValue(path, out string? hash))
            throw new IOException(ArchivePathPolicy.Text("SaveAsRequired"));
        string temporary = CreateWorkspace();
        try
        {
            string replacement = Path.Combine(temporary, "replacement");
            if(template is SecureFileTemplate)
            {
                using var plaintext = new MemoryStream();
                try
                {
                    if(markdown?.IsActive == true)
                    {
                        byte[] data = markdown.GetBytes();
                        try { await plaintext.WriteAsync(data, token); }
                        finally { CryptographicOperations.ZeroMemory(data); }
                    }
                    else await saver.SaveToStreamAsync(richTextBox, plaintext, new XamlPackageFileTemplate(), token, progress, snapshot);
                    plaintext.Position = 0;
                    await secureFiles.EncryptStreamAsync(plaintext, markdown?.IsActive == true ? ".md" : ".XamlPackage",
                        replacement, progress, token);
                }
                finally
                {
                    if(plaintext.TryGetBuffer(out var data)) CryptographicOperations.ZeroMemory(data.AsSpan());
                }
            }
            else
            {
                await using var output = new FileStream(replacement, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                await saver.SaveToStreamAsync(richTextBox, output, template, token, progress, snapshot);
            }
            await archives.ReplaceDocumentAsync(location, replacement, hash, progress, token);
            versions[path] = await archives.GetSourceHashAsync(location, CancellationToken.None);
        }
        finally { await ArchiveService.RemoveWorkspaceAsync(temporary); }
    }

    private static string CreateWorkspace()
    {
        return ArchiveWorkspace.CreateTransient("archive-document");
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs args)
    {
        if(documents is not IWorkspaceDocumentSession workspace) return;
        foreach(string path in versions.Keys.Where(path => !workspace.ContainsDocument(path)).ToArray())
        {
            versions.Remove(path);
            try { archives.CloseDocument(owner, StorageLocation.Parse(path)); }
            catch(DirectoryNotFoundException) { }
        }
    }

    public void Dispose()
    {
        documents.PropertyChanged -= OnDocumentChanged;
        archives.CloseOwner(owner);
    }
}

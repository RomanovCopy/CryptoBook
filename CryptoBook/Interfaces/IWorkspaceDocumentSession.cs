using System.ComponentModel;

namespace CryptoBook.Interfaces;

/// <summary>Independent Home and Markdown documents sharing the active file commands.</summary>
public interface IWorkspaceDocumentSession : INotifyPropertyChanged
{
    string ActivePageKey { get; }
    bool HasHomeDocument { get; }
    bool HasMarkdownDocument { get; }
    bool HasInactiveChanges { get; }
    bool SelectPage(string pageKey);
    IDisposable HoldActiveDocument();
    void CloseCurrent();
    WorkspaceDocumentSnapshot? CaptureInactiveDocument();
    void RestoreInactiveDocument(WorkspaceDocumentSnapshot? snapshot);
}

public sealed record WorkspaceDocumentSnapshot(
    string? FilePath,
    string DisplayName,
    string? TemplateId,
    long Revision,
    long SavedRevision,
    bool IsMarkdown,
    byte[] Content,
    bool IsReadOnly = false);

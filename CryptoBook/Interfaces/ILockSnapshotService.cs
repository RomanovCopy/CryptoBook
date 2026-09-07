using System.Windows.Documents;

namespace CryptoBook.Interfaces;

public sealed record LockSnapshotMetadata(
    string? OriginalPath,
    string DocumentName,
    string TemplateId,
    bool HasUnsavedChanges,
    DateTimeOffset CreatedUtc,
    string? ContentTemplateId = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public WorkspaceDocumentSnapshot? InactiveDocument { get; init; }
}

public interface ILockSnapshotService : IService
{
    string SnapshotPath { get; }
    bool Exists { get; }
    Task CreateAndVerifyAsync(
        IRichTextBoxService richTextBox,
        LockSnapshotMetadata metadata,
        CancellationToken cancellationToken = default);
    Task<(FlowDocument Document, LockSnapshotMetadata Metadata)> ReadAndVerifyAsync(
        CancellationToken cancellationToken = default);
    void Delete();
}

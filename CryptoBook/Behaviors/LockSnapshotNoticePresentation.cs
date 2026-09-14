using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Behaviors;

internal static class LockSnapshotNoticePresentation
{
    internal static string Describe(LockSnapshotNotice notice)
    {
        string title = LocalizationManager.Format("Security.RecoverySnapshotFrom", notice.SavedAt.ToLocalTime());
        string details = notice.Documents.Count == 0
            ? LocalizationManager.Format("Security.RecoveryUnknownSource", notice.SnapshotPath)
            : string.Join(Environment.NewLine, notice.Documents.Select(document =>
                LocalizationManager.Format("Security.RecoveryDocument", document.DocumentName,
                    string.IsNullOrWhiteSpace(document.OriginalPath)
                        ? LocalizationManager.GetString("Security.RecoveryUnsavedPath") : document.OriginalPath)));
        return title + Environment.NewLine + details;
    }
}

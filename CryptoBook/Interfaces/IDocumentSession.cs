using System.ComponentModel;

namespace CryptoBook.Interfaces
{
    public interface IDocumentSession:
        IService,
        INotifyPropertyChanged
    {
        string? FilePath { get; }
        string DisplayName { get; }
        IFileTemplate? Template { get; }
        bool IsDirty { get; }
        long Revision { get; }
        long SavedRevision { get; }

        void Open(string filePath, IFileTemplate template);
        void MarkDirty();
        void MarkSaved(string filePath, IFileTemplate template);
        void MarkSaved(
            string filePath,
            IFileTemplate template,
            long savedRevision);
        void Rename(string filePath);
        void SetDisplayName(string displayName);
    }
}

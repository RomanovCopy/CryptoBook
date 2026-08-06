using System.ComponentModel;
using System.Windows.Documents;

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
        bool HasDocument { get; }

        void Open(string filePath, IFileTemplate template);
        void Open(
            string filePath,
            IFileTemplate template,
            FlowDocument document);
        void Close();
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

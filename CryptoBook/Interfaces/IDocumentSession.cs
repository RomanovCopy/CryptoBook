using System.ComponentModel;

namespace CryptoBook.Interfaces
{
    public interface IDocumentSession:
        IService,
        INotifyPropertyChanged
    {
        string? FilePath { get; }
        IFileTemplate? Template { get; }
        bool IsDirty { get; }

        void Open(string filePath, IFileTemplate template);
        void MarkDirty();
        void MarkSaved(string filePath, IFileTemplate template);
    }
}

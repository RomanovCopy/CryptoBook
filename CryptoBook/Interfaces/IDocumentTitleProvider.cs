using System.ComponentModel;

namespace CryptoBook.Interfaces
{
    public interface IDocumentTitleProvider:
        INotifyPropertyChanged,
        IDisposable
    {
        string Title { get; }
        string? Path { get; }
    }
}

using CryptoBook.DTO;

using System.ComponentModel;
using System.Windows.Media;

namespace CryptoBook.Interfaces
{
    public interface IFilePreviewViewModel: INotifyPropertyChanged
    {
        FilePreviewKind PreviewKind { get; }
        string FileName { get; }
        string FileDetails { get; }
        string Text { get; }
        string Message { get; }
        ImageSource? Image { get; }
        Task SelectAsync(
            ISystemItem? item,
            CancellationToken cancellationToken = default);
        void Clear();
    }
}

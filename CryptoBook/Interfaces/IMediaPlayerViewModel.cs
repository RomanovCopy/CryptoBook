using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMediaPlayerViewModel: IViewModel, IWindowWithId, IDisposable
    {
        IMediaPlayerService VideoService { get; }
        IImageService ImageService { get; }
        bool IsVideoVisible { get; }
        bool IsImageVisible { get; }
        bool IsEmptyVisible { get; }
        string StatusText { get; }
        string MediaTitle { get; }

        ICommand OpenFileCommand { get; }
        ICommand RotateImageCommand { get; }
        ICommand ResetImageTransformCommand { get; }
        ICommand PreviousImageCommand { get; }
        ICommand NextImageCommand { get; }
        ICommand PreviousVideoCommand { get; }
        ICommand NextVideoCommand { get; }
        ICommand DeleteCurrentImageCommand { get; }
    }
}

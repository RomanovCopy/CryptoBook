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
        bool IsSynchronizationEnabled { get; set; }

        ICommand OpenFileCommand { get; }
        ICommand OpenFileInNewWindowCommand { get; }
        ICommand PauseAllCommand { get; }
        ICommand ToggleSynchronizationCommand { get; }
        ICommand ActivatedCommand { get; }
        ICommand DeactivatedCommand { get; }
        ICommand RotateImageCommand { get; }
        ICommand ResetImageTransformCommand { get; }
        ICommand PreviousImageCommand { get; }
        ICommand NextImageCommand { get; }
        ICommand PreviousVideoCommand { get; }
        ICommand NextVideoCommand { get; }
        ICommand DeleteCurrentImageCommand { get; }
    }
}

namespace CryptoBook.Interfaces
{
    public interface IMediaPlayerModel: IModel, IWindowWithId, IDisposable
    {
        IMediaPlayerService VideoService { get; }
        IImageService ImageService { get; }
        bool IsVideoVisible { get; }
        bool IsImageVisible { get; }
        bool IsEmptyVisible { get; }
        string StatusText { get; }
        string MediaTitle { get; }

        bool CanExecute_OpenFile(object? obj);
        void Execute_OpenFile(object? obj);
        bool CanExecute_RotateImage(object? obj);
        void Execute_RotateImage(object? obj);
        bool CanExecute_ResetImageTransform(object? obj);
        void Execute_ResetImageTransform(object? obj);
        bool CanExecute_PreviousImage(object? obj);
        void Execute_PreviousImage(object? obj);
        bool CanExecute_NextImage(object? obj);
        void Execute_NextImage(object? obj);
        bool CanExecute_PreviousVideo(object? obj);
        void Execute_PreviousVideo(object? obj);
        bool CanExecute_NextVideo(object? obj);
        void Execute_NextVideo(object? obj);
        bool CanExecute_DeleteCurrentImage(object? obj);
        void Execute_DeleteCurrentImage(object? obj);
    }
}

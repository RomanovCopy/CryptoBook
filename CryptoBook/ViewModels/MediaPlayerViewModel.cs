using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class MediaPlayerViewModel: ViewModelBase, IMediaPlayerViewModel
    {
        private readonly IMediaPlayerModel _model;

        public MediaPlayerViewModel(IMediaPlayerModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _model.PropertyChanged += OnModelPropertyChanged;
        }

        public Guid WindowId => _model.WindowId;
        public IMediaPlayerService VideoService => _model.VideoService;
        public IImageService ImageService => _model.ImageService;
        public bool IsVideoVisible => _model.IsVideoVisible;
        public bool IsImageVisible => _model.IsImageVisible;
        public bool IsEmptyVisible => _model.IsEmptyVisible;
        public string StatusText => _model.StatusText;
        public string MediaTitle => _model.MediaTitle;

        public ICommand OpenFileCommand => _openFileCommand ??=
            new RelayCommand(_model.Execute_OpenFile, _model.CanExecute_OpenFile);
        private RelayCommand? _openFileCommand;

        public ICommand RotateImageCommand => _rotateImageCommand ??=
            new RelayCommand(_model.Execute_RotateImage, _model.CanExecute_RotateImage);
        private RelayCommand? _rotateImageCommand;

        public ICommand ResetImageTransformCommand => _resetImageTransformCommand ??=
            new RelayCommand(
                _model.Execute_ResetImageTransform,
                _model.CanExecute_ResetImageTransform);
        private RelayCommand? _resetImageTransformCommand;

        public ICommand PreviousImageCommand => _previousImageCommand ??=
            new RelayCommand(
                _model.Execute_PreviousImage,
                _model.CanExecute_PreviousImage);
        private RelayCommand? _previousImageCommand;

        public ICommand NextImageCommand => _nextImageCommand ??=
            new RelayCommand(
                _model.Execute_NextImage,
                _model.CanExecute_NextImage);
        private RelayCommand? _nextImageCommand;

        public ICommand PreviousVideoCommand => _previousVideoCommand ??=
            new RelayCommand(
                _model.Execute_PreviousVideo,
                _model.CanExecute_PreviousVideo);
        private RelayCommand? _previousVideoCommand;

        public ICommand NextVideoCommand => _nextVideoCommand ??=
            new RelayCommand(
                _model.Execute_NextVideo,
                _model.CanExecute_NextVideo);
        private RelayCommand? _nextVideoCommand;

        public ICommand DeleteCurrentImageCommand => _deleteCurrentImageCommand ??=
            new RelayCommand(
                _model.Execute_DeleteCurrentImage,
                _model.CanExecute_DeleteCurrentImage);
        private RelayCommand? _deleteCurrentImageCommand;

        public ICommand Loaded => _loadedCommand ??=
            new RelayCommand(_model.Execute_Loaded, _model.CanExecute_Loaded);
        private RelayCommand? _loadedCommand;

        public ICommand Close => _closeCommand ??=
            new RelayCommand(_model.Execute_Close, _model.CanExecute_Close);
        private RelayCommand? _closeCommand;

        public ICommand Closing => _closingCommand ??=
            new RelayCommand(_model.Execute_Closing, _model.CanExecute_Closing);
        private RelayCommand? _closingCommand;

        public ICommand Closed => _closedCommand ??=
            new RelayCommand(_model.Execute_Closed, _model.CanExecute_Closed);
        private RelayCommand? _closedCommand;

        private void OnModelPropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e) =>
            OnPropertyChanged(e.PropertyName ?? string.Empty);

        public void Dispose()
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
            _model.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

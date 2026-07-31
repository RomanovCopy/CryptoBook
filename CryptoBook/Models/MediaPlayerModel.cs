using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.Models
{
    public sealed class MediaPlayerModel: ViewModelBase, IMediaPlayerModel
    {
        private static readonly HashSet<string> ImageExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".bmp", ".gif", ".ico", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
            };

        private CancellationTokenSource? _openCancellation;
        private readonly IFileManagerService _fileManagerService;
        private readonly IMessageService _messageService;
        private IReadOnlyList<string> _imagePaths = Array.Empty<string>();
        private int _currentImageIndex = -1;
        private bool _isClosing;
        private bool _disposed;
        private bool _isVideoVisible;
        private bool _isImageVisible;
        private bool _isEmptyVisible = true;
        private string _statusText = "Откройте изображение или видео";
        private string _mediaTitle = "Медиаплеер";

        public MediaPlayerModel(
            IMediaPlayerService videoService,
            IImageService imageService,
            IWindowContext windowContext,
            IFileManagerService fileManagerService,
            IMessageService messageService)
        {
            VideoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
            ImageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _fileManagerService = fileManagerService ??
                throw new ArgumentNullException(nameof(fileManagerService));
            _messageService = messageService ??
                throw new ArgumentNullException(nameof(messageService));

            VideoService.MediaFailed += OnMediaFailed;

            if(windowContext.TryGet<string>("path", out var initialPath) &&
                !string.IsNullOrWhiteSpace(initialPath))
            {
                // Flyleaf должен получить полностью созданное визуальное дерево окна.
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    async () => await OpenPathAsync(initialPath));
            }
        }

        public Guid WindowId { get; } = Guid.NewGuid();
        public IMediaPlayerService VideoService { get; }
        public IImageService ImageService { get; }
        public bool IsVideoVisible => _isVideoVisible;
        public bool IsImageVisible => _isImageVisible;
        public bool IsEmptyVisible => _isEmptyVisible;
        public string StatusText => _statusText;
        public string MediaTitle => _mediaTitle;

        public bool CanExecute_OpenFile(object? obj) => !_disposed && !_isClosing;
        public void Execute_OpenFile(object? obj) => _ = OpenFileAsync();

        public bool CanExecute_RotateImage(object? obj) =>
            !_disposed && !_isClosing && ImageService.ImageSource != null;

        public void Execute_RotateImage(object? obj) => ImageService.RotateRight();

        public bool CanExecute_ResetImageTransform(object? obj) =>
            !_disposed && !_isClosing && ImageService.ImageSource != null;

        public void Execute_ResetImageTransform(object? obj) => ImageService.ResetTransform();

        public bool CanExecute_PreviousImage(object? obj) =>
            !_disposed && !_isClosing && _isImageVisible && _currentImageIndex > 0;

        public void Execute_PreviousImage(object? obj) => _ = NavigateImageAsync(-1);

        public bool CanExecute_NextImage(object? obj) =>
            !_disposed &&
            !_isClosing &&
            _isImageVisible &&
            _currentImageIndex >= 0 &&
            _currentImageIndex < _imagePaths.Count - 1;

        public void Execute_NextImage(object? obj) => _ = NavigateImageAsync(1);

        public bool CanExecute_DeleteCurrentImage(object? obj) =>
            !_disposed &&
            !_isClosing &&
            _isImageVisible &&
            _currentImageIndex >= 0 &&
            _currentImageIndex < _imagePaths.Count;

        public void Execute_DeleteCurrentImage(object? obj) =>
            _ = DeleteCurrentImageAsync();

        public bool CanExecute_Loaded(object? obj) => true;
        public void Execute_Loaded(object? obj) { }
        public bool CanExecute_Close(object? obj) => true;
        public void Execute_Close(object? obj) { }
        public bool CanExecute_Closing(object? obj) => !_isClosing && !_disposed;

        public void Execute_Closing(object? obj)
        {
            if(obj is CancelEventArgs { Cancel: true })
                return;

            _isClosing = true;
            CancelPendingOpen();
            CommandManager.InvalidateRequerySuggested();
        }

        public bool CanExecute_Closed(object? obj) => !_disposed;
        public void Execute_Closed(object? obj) => Dispose();

        private async Task OpenFileAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Открыть изображение или видео",
                Filter =
                    "Медиафайлы|*.bmp;*.gif;*.ico;*.jpeg;*.jpg;*.png;*.tif;*.tiff;*.webp;*.avi;*.mkv;*.mov;*.mp4;*.m4v;*.mpeg;*.mpg;*.ts;*.webm;*.wmv|" +
                    "Изображения|*.bmp;*.gif;*.ico;*.jpeg;*.jpg;*.png;*.tif;*.tiff;*.webp|" +
                    "Видео|*.avi;*.mkv;*.mov;*.mp4;*.m4v;*.mpeg;*.mpg;*.ts;*.webm;*.wmv|" +
                    "Все файлы|*.*"
            };

            if(dialog.ShowDialog() == true)
                await OpenPathAsync(dialog.FileName);
        }

        private async Task OpenPathAsync(string path)
        {
            if(_disposed || _isClosing)
                return;

            CancelPendingOpen();
            _openCancellation = new CancellationTokenSource();
            var token = _openCancellation.Token;

            SetStatus($"Загрузка: {Path.GetFileName(path)}");
            SetProperty(ref _mediaTitle, Path.GetFileName(path), nameof(MediaTitle));

            try
            {
                if(ImageExtensions.Contains(Path.GetExtension(path)))
                {
                    VideoService.Stop();
                    await ImageService.LoadImageAsync(path, token);
                    token.ThrowIfCancellationRequested();
                    if(_disposed || _isClosing)
                        return;

                    if(ImageService.ImageSource == null)
                        throw new InvalidOperationException("Не удалось декодировать изображение.");

                    UpdateImageSequence(path);
                    SetMode(isImage: true);
                }
                else
                {
                    ClearImageSequence();
                    ImageService.Clear();
                    SetMode(isImage: false);
                    await VideoService.OpenAsync(path, autoPlay: true, token);
                    token.ThrowIfCancellationRequested();
                    if(_disposed || _isClosing)
                        return;
                }

                SetStatus(string.Empty);
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                if(_disposed || _isClosing)
                    return;

                ImageService.Clear();
                VideoService.Stop();
                SetEmpty(ex.Message);
            }
        }

        private void SetMode(bool isImage)
        {
            _isImageVisible = isImage;
            _isVideoVisible = !isImage;
            _isEmptyVisible = false;
            OnPropertyChanged(
                nameof(IsImageVisible),
                nameof(IsVideoVisible),
                nameof(IsEmptyVisible));
            CommandManager.InvalidateRequerySuggested();
        }

        private void SetEmpty(string message)
        {
            ClearImageSequence();
            _isImageVisible = false;
            _isVideoVisible = false;
            _isEmptyVisible = true;
            SetStatus(message);
            OnPropertyChanged(
                nameof(IsImageVisible),
                nameof(IsVideoVisible),
                nameof(IsEmptyVisible));
        }

        private void SetStatus(string value) =>
            SetProperty(ref _statusText, value, nameof(StatusText));

        private async Task NavigateImageAsync(int offset)
        {
            var targetIndex = _currentImageIndex + offset;
            if(targetIndex < 0 || targetIndex >= _imagePaths.Count)
                return;

            await OpenPathAsync(_imagePaths[targetIndex]);
        }

        private async Task DeleteCurrentImageAsync()
        {
            if(!CanExecute_DeleteCurrentImage(null))
                return;

            var deletedIndex = _currentImageIndex;
            var path = _imagePaths[deletedIndex];
            var dialogId = await _messageService.ShowMessage(
                "Удаление изображения",
                $"Удалить «{Path.GetFileName(path)}»?",
                isCanceled: true);

            if(!_messageService.ShowConfirmation(dialogId) || _disposed || _isClosing)
                return;

            var result = await _fileManagerService.DeleteAsync(path);
            if(_disposed || _isClosing)
                return;

            if(!result.Success)
            {
                await _messageService.ShowMessage(
                    "Ошибка удаления",
                    result.ErrorMessage ?? "Не удалось удалить изображение.");
                return;
            }

            var remainingImages = _imagePaths
                .Where(file => !string.Equals(
                    Path.GetFullPath(file),
                    Path.GetFullPath(path),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if(remainingImages.Length == 0)
            {
                ImageService.Clear();
                ClearImageSequence();
                SetProperty(ref _mediaTitle, "Медиаплеер", nameof(MediaTitle));
                SetEmpty("В папке больше нет изображений.");
                return;
            }

            var nextIndex = Math.Min(deletedIndex, remainingImages.Length - 1);
            await OpenPathAsync(remainingImages[nextIndex]);
        }

        private void UpdateImageSequence(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            try
            {
                _imagePaths = string.IsNullOrWhiteSpace(directory)
                    ? [fullPath]
                    : Directory.EnumerateFiles(directory)
                        .Where(file => ImageExtensions.Contains(Path.GetExtension(file)))
                        .OrderBy(
                            file => Path.GetFileName(file),
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
            }
            catch(IOException)
            {
                _imagePaths = [fullPath];
            }
            catch(UnauthorizedAccessException)
            {
                _imagePaths = [fullPath];
            }

            _currentImageIndex = FindImageIndex(fullPath);
            CommandManager.InvalidateRequerySuggested();
        }

        private int FindImageIndex(string fullPath)
        {
            for(var index = 0; index < _imagePaths.Count; index++)
            {
                if(string.Equals(
                    Path.GetFullPath(_imagePaths[index]),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private void ClearImageSequence()
        {
            _imagePaths = Array.Empty<string>();
            _currentImageIndex = -1;
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnMediaFailed(object? sender, string error)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if(dispatcher?.CheckAccess() == false)
            {
                dispatcher.BeginInvoke(() =>
                {
                    if(!_disposed && !_isClosing)
                        SetEmpty(error);
                });
            }
            else if(!_disposed && !_isClosing)
            {
                SetEmpty(error);
            }
        }

        private void CancelPendingOpen()
        {
            _openCancellation?.Cancel();
            _openCancellation?.Dispose();
            _openCancellation = null;
        }

        public void Dispose()
        {
            if(_disposed)
                return;

            _disposed = true;
            CancelPendingOpen();
            VideoService.MediaFailed -= OnMediaFailed;
            ImageService.Clear();
            VideoService.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

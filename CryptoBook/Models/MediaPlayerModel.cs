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

        private static readonly HashSet<string> SecureFileExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".cbook", ".cbox"
            };

        private CancellationTokenSource? _openCancellation;
        private readonly IFileManagerService _fileManagerService;
        private readonly IMessageService _messageService;
        private readonly IMediaSourcePreparationService _mediaSourcePreparationService;
        private readonly IFilePickerService _filePickerService;
        private readonly List<IPreparedMediaSource> _retiredSources = [];
        private IPreparedMediaSource? _activeSource;
        private IReadOnlyList<string> _imagePaths = Array.Empty<string>();
        private int _currentImageIndex = -1;
        private bool _isClosing;
        private bool _disposed;
        private bool _isVideoVisible;
        private bool _isImageVisible;
        private bool _isEmptyVisible = true;
        private string _statusText =
            LocalizationManager.GetString("Media.DefaultStatus");
        private string _mediaTitle =
            LocalizationManager.GetString("Media.PlayerTitle");
        private string? _statusResourceKey = "Media.DefaultStatus";
        private object?[] _statusArguments = [];
        private bool _usesDefaultTitle = true;

        public MediaPlayerModel(
            IMediaPlayerService videoService,
            IImageService imageService,
            IWindowContext windowContext,
            IFileManagerService fileManagerService,
            IMessageService messageService,
            IMediaSourcePreparationService mediaSourcePreparationService,
            IFilePickerService filePickerService)
        {
            VideoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
            ImageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _fileManagerService = fileManagerService ??
                throw new ArgumentNullException(nameof(fileManagerService));
            _messageService = messageService ??
                throw new ArgumentNullException(nameof(messageService));
            _mediaSourcePreparationService = mediaSourcePreparationService ??
                throw new ArgumentNullException(nameof(mediaSourcePreparationService));
            _filePickerService = filePickerService ??
                throw new ArgumentNullException(nameof(filePickerService));

            VideoService.MediaFailed += OnMediaFailed;
            LocalizationManager.CultureChanged += OnCultureChanged;

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
        public void Execute_OpenFile(object? obj) => _ = PickAndOpenFileAsync();

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

        private async Task PickAndOpenFileAsync()
        {
            string? initialDirectory = _activeSource?.OriginalPath is { Length: > 0 } path
                ? Path.GetDirectoryName(path)
                : null;

            try
            {
                string? selectedPath = await _filePickerService.PickFileAsync(
                    initialDirectory,
                    CancellationToken.None);
                if(!string.IsNullOrWhiteSpace(selectedPath) && !_disposed && !_isClosing)
                    await OpenPathAsync(selectedPath);
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                if(!_disposed && !_isClosing)
                    SetEmpty(ex.Message);
            }
        }

        private async Task OpenPathAsync(string path)
        {
            if(_disposed || _isClosing)
                return;

            CancelPendingOpen();
            _openCancellation = new CancellationTokenSource();
            var token = _openCancellation.Token;

            SetLocalizedStatus("Media.Loading", Path.GetFileName(path));
            _usesDefaultTitle = false;
            SetProperty(ref _mediaTitle, Path.GetFileName(path), nameof(MediaTitle));

            IPreparedMediaSource? preparedSource = null;
            try
            {
                preparedSource = await _mediaSourcePreparationService.PrepareAsync(path, token);
                string playbackPath = preparedSource.PlaybackPath;

                if(ImageExtensions.Contains(Path.GetExtension(playbackPath)))
                {
                    VideoService.Stop();
                    await ImageService.LoadImageAsync(playbackPath, token);
                    token.ThrowIfCancellationRequested();
                    if(_disposed || _isClosing)
                        return;

                    if(ImageService.ImageSource == null)
                        throw new InvalidOperationException(
                            LocalizationManager.GetString("Media.DecodeFailed"));

                    UpdateImageSequence(path, preparedSource.IsTemporary);
                    SetMode(isImage: true);
                }
                else
                {
                    ClearImageSequence();
                    ImageService.Clear();
                    SetMode(isImage: false);
                    await VideoService.OpenAsync(playbackPath, autoPlay: true, token);
                    token.ThrowIfCancellationRequested();
                    if(_disposed || _isClosing)
                        return;
                }

                ReplaceActiveSource(preparedSource);
                preparedSource = null;
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
            finally
            {
                preparedSource?.Dispose();
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
            SetRawStatus(value);

        private void SetRawStatus(string value)
        {
            _statusResourceKey = null;
            _statusArguments = [];
            SetProperty(ref _statusText, value, nameof(StatusText));
        }

        private void SetLocalizedStatus(string key, params object?[] arguments)
        {
            _statusResourceKey = key;
            _statusArguments = arguments;
            SetProperty(
                ref _statusText,
                LocalizationManager.Format(key, arguments),
                nameof(StatusText));
        }

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
                LocalizationManager.GetString("Media.DeleteTitle"),
                LocalizationManager.Format(
                    "Media.DeletePrompt",
                    Path.GetFileName(path)),
                isCanceled: true);

            if(!_messageService.ShowConfirmation(dialogId) || _disposed || _isClosing)
                return;

            var result = await _fileManagerService.DeleteAsync(path);
            if(_disposed || _isClosing)
                return;

            if(!result.Success)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Media.DeleteError"),
                    result.ErrorMessage ?? LocalizationManager.GetString(
                        "Media.DeleteFailed"));
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
                _usesDefaultTitle = true;
                SetProperty(
                    ref _mediaTitle,
                    LocalizationManager.GetString("Media.PlayerTitle"),
                    nameof(MediaTitle));
                SetEmpty(LocalizationManager.GetString("Media.NoMoreImages"));
                SetLocalizedStatus("Media.NoMoreImages");
                return;
            }

            var nextIndex = Math.Min(deletedIndex, remainingImages.Length - 1);
            await OpenPathAsync(remainingImages[nextIndex]);
        }

        private void UpdateImageSequence(string path, bool isEncrypted)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            try
            {
                _imagePaths = string.IsNullOrWhiteSpace(directory)
                    ? [fullPath]
                    : Directory.EnumerateFiles(directory)
                        .Where(file => IsImageSequenceCandidate(file, isEncrypted))
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

        internal static bool IsImageSequenceCandidate(string path, bool includeSecureFiles)
        {
            string extension = Path.GetExtension(path);
            return ImageExtensions.Contains(extension) ||
                includeSecureFiles && SecureFileExtensions.Contains(extension);
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

        private void ReplaceActiveSource(IPreparedMediaSource source)
        {
            if(_activeSource is not null)
            {
                _activeSource.Dispose();
                string? directory = Path.GetDirectoryName(_activeSource.PlaybackPath);
                if(_activeSource.IsTemporary &&
                   directory is not null &&
                   Directory.Exists(directory))
                {
                    _retiredSources.Add(_activeSource);
                }
            }

            _activeSource = source;
        }

        public void Dispose()
        {
            if(_disposed)
                return;

            _disposed = true;
            CancelPendingOpen();
            VideoService.MediaFailed -= OnMediaFailed;
            LocalizationManager.CultureChanged -= OnCultureChanged;
            ImageService.Clear();
            VideoService.Dispose();
            _activeSource?.Dispose();
            foreach(var source in _retiredSources)
                source.Dispose();
            _retiredSources.Clear();
            GC.SuppressFinalize(this);
        }

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            if(_statusResourceKey is not null)
            {
                SetProperty(
                    ref _statusText,
                    LocalizationManager.Format(
                        _statusResourceKey,
                        _statusArguments),
                    nameof(StatusText));
            }

            if(_usesDefaultTitle)
            {
                SetProperty(
                    ref _mediaTitle,
                    LocalizationManager.GetString("Media.PlayerTitle"),
                    nameof(MediaTitle));
            }
        }
    }
}

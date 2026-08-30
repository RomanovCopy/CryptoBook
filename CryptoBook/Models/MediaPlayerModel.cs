using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

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

        private static readonly HashSet<string> VideoExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".avi", ".mkv", ".mov", ".mp4", ".m4v", ".mpeg", ".mpg", ".ts", ".webm", ".wmv"
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
        private readonly IFileExplorerService _fileExplorerService;
        private readonly IWindowManager _windowManager;
        private readonly IMediaPlaybackCoordinator _playbackCoordinator;
        private readonly int _instanceNumber;
        private readonly List<IPreparedMediaSource> _retiredSources = [];
        private IPreparedMediaSource? _activeSource;
        private IReadOnlyList<string> _imagePaths = Array.Empty<string>();
        private int _currentImageIndex = -1;
        private IReadOnlyList<string> _videoPaths = Array.Empty<string>();
        private int _currentVideoIndex = -1;
        private IReadOnlyList<string> _catalogPaths = Array.Empty<string>();
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
        private string? _currentDisplayName;

        public MediaPlayerModel(
            IMediaPlayerService videoService,
            IImageService imageService,
            IWindowContext windowContext,
            IFileManagerService fileManagerService,
            IMessageService messageService,
            IMediaSourcePreparationService mediaSourcePreparationService,
            IFileExplorerService fileExplorerService,
            IWindowManager windowManager,
            IMediaPlaybackCoordinator playbackCoordinator)
        {
            VideoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
            ImageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _fileManagerService = fileManagerService ??
                throw new ArgumentNullException(nameof(fileManagerService));
            _messageService = messageService ??
                throw new ArgumentNullException(nameof(messageService));
            _mediaSourcePreparationService = mediaSourcePreparationService ??
                throw new ArgumentNullException(nameof(mediaSourcePreparationService));
            _fileExplorerService = fileExplorerService ??
                throw new ArgumentNullException(nameof(fileExplorerService));
            _windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            _playbackCoordinator = playbackCoordinator ??
                throw new ArgumentNullException(nameof(playbackCoordinator));

            VideoService.MediaFailed += OnMediaFailed;
            _playbackCoordinator.PropertyChanged += OnPlaybackCoordinatorPropertyChanged;
            LocalizationManager.CultureChanged += OnCultureChanged;
            _instanceNumber = _playbackCoordinator.Register(WindowId, VideoService);
            UpdateMediaTitle();

            string? catalogSelectionPath = null;
            if(windowContext.TryGet<MediaCatalogSelection>(
                MediaCatalogSelection.WindowContextKey,
                out MediaCatalogSelection catalogSelection))
            {
                ApplyCatalog(catalogSelection);
                catalogSelectionPath = catalogSelection.SelectedPath;
            }

            if(windowContext.TryGet<string>("path", out var initialPath) &&
                !string.IsNullOrWhiteSpace(initialPath))
            {
                // Flyleaf должен получить полностью созданное визуальное дерево окна.
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    async () => await OpenPathAsync(
                        initialPath,
                        autoPlay: true,
                        catalogSelectionPath));
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
        public bool IsSynchronizationEnabled
        {
            get => _playbackCoordinator.IsSynchronizationEnabled;
            set => _playbackCoordinator.IsSynchronizationEnabled = value;
        }

        public bool CanExecute_OpenFile(object? obj) => !_disposed && !_isClosing;
        public void Execute_OpenFile(object? obj) => ShowFileExplorer(
            selection => _ = OpenSelectedFileAsync(selection));

        public bool CanExecute_OpenFileInNewWindow(object? obj) =>
            !_disposed && !_isClosing;

        public void Execute_OpenFileInNewWindow(object? obj) =>
            ShowFileExplorer(OpenFileInNewWindow);

        public bool CanExecute_PauseAll(object? obj) => !_disposed;
        public void Execute_PauseAll(object? obj) =>
            _playbackCoordinator.PauseAll();

        public bool CanExecute_ToggleSynchronization(object? obj) => !_disposed;
        public void Execute_ToggleSynchronization(object? obj) =>
            IsSynchronizationEnabled = !IsSynchronizationEnabled;

        public bool CanExecute_Activated(object? obj) => !_disposed && !_isClosing;
        public void Execute_Activated(object? obj) =>
            _playbackCoordinator.Activate(WindowId);

        public bool CanExecute_Deactivated(object? obj) => !_disposed;
        public void Execute_Deactivated(object? obj) =>
            _playbackCoordinator.Deactivate(WindowId);

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

        public bool CanExecute_PreviousVideo(object? obj) =>
            !_disposed && !_isClosing && _isVideoVisible && _currentVideoIndex > 0;

        public void Execute_PreviousVideo(object? obj) =>
            _ = NavigateVideoAsync(-1, autoPlay: VideoService.IsPlaying);

        public bool CanExecute_NextVideo(object? obj) =>
            !_disposed &&
            !_isClosing &&
            _isVideoVisible &&
            _currentVideoIndex >= 0 &&
            _currentVideoIndex < _videoPaths.Count - 1;

        public void Execute_NextVideo(object? obj) =>
            _ = NavigateVideoAsync(1, autoPlay: VideoService.IsPlaying);

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

        private void ShowFileExplorer(Action<MediaCatalogSelection> fileSelected)
        {
            try
            {
                _fileExplorerService.ShowFileSelection(
                    GetInitialDirectory(),
                    selection =>
                    {
                        if(!_disposed && !_isClosing)
                            fileSelected(selection);
                    });
            }
            catch(Exception ex)
            {
                if(!_disposed && !_isClosing)
                    SetEmpty(ex.Message);
            }
        }

        private void OpenFileInNewWindow(MediaCatalogSelection selection)
        {
            string selectedPath = selection.SelectedPath;
            if(string.IsNullOrWhiteSpace(selectedPath) || _disposed || _isClosing)
                return;

            try
            {
                var context = new Dictionary<string, object?>
                {
                    ["path"] = selectedPath
                };
                if(selection.FilePaths.Count > 0)
                {
                    context[MediaCatalogSelection.WindowContextKey] = selection;
                }
                Guid windowId = _windowManager.CreateSiblingWindow<MediaPlayer>(context);
                _windowManager.ShowWindow(windowId);
            }
            catch(Exception ex)
            {
                if(!_disposed && !_isClosing)
                    SetEmpty(ex.Message);
            }
        }

        private async Task OpenSelectedFileAsync(MediaCatalogSelection selection)
        {
            if(_disposed || _isClosing)
                return;

            ApplyCatalog(selection);
            // FileExplorer является соседним окном и остаётся открытым позади.
            // Сразу возвращаем MediaPlayer на передний план, пока файл загружается.
            _windowManager.ActivateWindow(WindowId);
            await OpenPathAsync(
                selection.SelectedPath,
                autoPlay: true,
                selection.SelectedPath);
        }

        private string? GetInitialDirectory() =>
            _activeSource?.OriginalPath is { Length: > 0 } path
                ? Path.GetDirectoryName(path)
                : null;

        private async Task OpenPathAsync(
            string path,
            bool autoPlay = false,
            string? sequencePath = null)
        {
            if(_disposed || _isClosing)
                return;

            CancelPendingOpen();
            _openCancellation = new CancellationTokenSource();
            var token = _openCancellation.Token;

            SetLocalizedStatus("Media.Loading", Path.GetFileName(path));
            _usesDefaultTitle = false;
            _currentDisplayName = Path.GetFileName(path);
            UpdateMediaTitle();

            IPreparedMediaSource? preparedSource = null;
            try
            {
                preparedSource = await _mediaSourcePreparationService.PrepareAsync(path, token);
                string playbackPath = preparedSource.PlaybackPath;

                if(ImageExtensions.Contains(Path.GetExtension(playbackPath)))
                {
                    VideoService.Stop();
                    ClearVideoSequence();
                    await ImageService.LoadImageAsync(playbackPath, token);
                    token.ThrowIfCancellationRequested();
                    if(_disposed || _isClosing)
                        return;

                    if(ImageService.ImageSource == null)
                        throw new InvalidOperationException(
                            LocalizationManager.GetString("Media.DecodeFailed"));

                    string catalogPath = sequencePath ?? path;
                    bool includeSecureFiles = preparedSource.IsTemporary ||
                        SecureFileExtensions.Contains(Path.GetExtension(catalogPath));
                    UpdateImageSequence(catalogPath, includeSecureFiles);
                    SetMode(isImage: true);
                }
                else
                {
                    ClearImageSequence();
                    ImageService.Clear();
                    SetMode(isImage: false);
                    await VideoService.OpenAsync(playbackPath, autoPlay, token);
                    token.ThrowIfCancellationRequested();
                    if(_disposed || _isClosing)
                        return;

                    string catalogPath = sequencePath ?? path;
                    bool includeSecureFiles = preparedSource.IsTemporary ||
                        SecureFileExtensions.Contains(Path.GetExtension(catalogPath));
                    UpdateVideoSequence(catalogPath, includeSecureFiles);
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
            ClearVideoSequence();
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

        private async Task NavigateVideoAsync(int offset, bool autoPlay)
        {
            var targetIndex = _currentVideoIndex + offset;
            if(targetIndex < 0 || targetIndex >= _videoPaths.Count)
                return;

            await OpenPathAsync(_videoPaths[targetIndex], autoPlay);
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
            RemoveFromCatalog(path);

            if(remainingImages.Length == 0)
            {
                ImageService.Clear();
                ClearImageSequence();
                _usesDefaultTitle = true;
                _currentDisplayName = null;
                UpdateMediaTitle();
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
                _imagePaths = BuildImageSequence(
                    fullPath,
                    directory,
                    isEncrypted,
                    _catalogPaths);
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

        private void UpdateVideoSequence(string path, bool isEncrypted)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            try
            {
                _videoPaths = BuildVideoSequence(
                    fullPath,
                    directory,
                    isEncrypted,
                    _catalogPaths);
            }
            catch(IOException)
            {
                _videoPaths = [fullPath];
            }
            catch(UnauthorizedAccessException)
            {
                _videoPaths = [fullPath];
            }

            _currentVideoIndex = FindMediaIndex(_videoPaths, fullPath);
            CommandManager.InvalidateRequerySuggested();
        }

        internal static bool IsVideoSequenceCandidate(string path, bool includeSecureFiles)
        {
            string extension = Path.GetExtension(path);
            return VideoExtensions.Contains(extension) ||
                includeSecureFiles && SecureFileExtensions.Contains(extension);
        }

        internal static IReadOnlyList<string> BuildImageSequence(
            string fullPath,
            string? directory,
            bool includeSecureFiles,
            IReadOnlyList<string>? catalogPaths) =>
            BuildSequence(
                fullPath,
                directory,
                catalogPaths,
                path => IsImageSequenceCandidate(path, includeSecureFiles));

        internal static IReadOnlyList<string> BuildVideoSequence(
            string fullPath,
            string? directory,
            bool includeSecureFiles,
            IReadOnlyList<string>? catalogPaths) =>
            BuildSequence(
                fullPath,
                directory,
                catalogPaths,
                path => IsVideoSequenceCandidate(path, includeSecureFiles));

        private static IReadOnlyList<string> BuildSequence(
            string fullPath,
            string? directory,
            IReadOnlyList<string>? catalogPaths,
            Func<string, bool> isCandidate)
        {
            if(catalogPaths is { Count: > 0 })
            {
                // Плоская коллекция FileExplorer уже отсортирована и может
                // объединять файлы из разных физических директорий.
                string[] catalog = catalogPaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(Path.GetFullPath)
                    .Where(isCandidate)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return catalog.Length > 0 ? catalog : [fullPath];
            }

            return string.IsNullOrWhiteSpace(directory)
                ? [fullPath]
                : Directory.EnumerateFiles(directory)
                    .Where(isCandidate)
                    .OrderBy(
                        file => Path.GetFileName(file),
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
        }

        private void ApplyCatalog(MediaCatalogSelection selection)
        {
            if(selection.FilePaths.Count == 0)
            {
                _catalogPaths = Array.Empty<string>();
                return;
            }

            IEnumerable<string> paths = selection.FilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path));
            if(!paths.Contains(
                selection.SelectedPath,
                StringComparer.OrdinalIgnoreCase))
            {
                paths = paths.Append(selection.SelectedPath);
            }

            _catalogPaths = paths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void RemoveFromCatalog(string path)
        {
            if(_catalogPaths.Count == 0)
                return;

            string fullPath = Path.GetFullPath(path);
            _catalogPaths = _catalogPaths
                .Where(candidate => !string.Equals(
                    Path.GetFullPath(candidate),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private int FindImageIndex(string fullPath)
            => FindMediaIndex(_imagePaths, fullPath);

        private static int FindMediaIndex(
            IReadOnlyList<string> paths,
            string fullPath)
        {
            for(var index = 0; index < paths.Count; index++)
            {
                if(string.Equals(
                    Path.GetFullPath(paths[index]),
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

        private void ClearVideoSequence()
        {
            _videoPaths = Array.Empty<string>();
            _currentVideoIndex = -1;
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
            _playbackCoordinator.PropertyChanged -= OnPlaybackCoordinatorPropertyChanged;
            LocalizationManager.CultureChanged -= OnCultureChanged;
            _playbackCoordinator.Unregister(WindowId);
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

            UpdateMediaTitle();
        }

        private void OnPlaybackCoordinatorPropertyChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName == nameof(
                IMediaPlaybackCoordinator.IsSynchronizationEnabled))
            {
                OnPropertyChanged(nameof(IsSynchronizationEnabled));
            }
        }

        private void UpdateMediaTitle()
        {
            string title = _usesDefaultTitle ||
                           string.IsNullOrWhiteSpace(_currentDisplayName)
                ? LocalizationManager.Format(
                    "Media.PlayerTitleNumbered",
                    _instanceNumber)
                : LocalizationManager.Format(
                    "Media.WindowTitle",
                    _currentDisplayName,
                    _instanceNumber);
            SetProperty(ref _mediaTitle, title, nameof(MediaTitle));
        }
    }
}

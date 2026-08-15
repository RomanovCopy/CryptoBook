using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.ViewModels
{
    public sealed class FilePreviewViewModel:
        ViewModelBase,
        IFilePreviewViewModel
    {
        private readonly IFilePreviewService _previewService;
        private CancellationTokenSource? _selectionCancellation;
        private FilePreviewKind _kind = FilePreviewKind.Empty;
        private string _fileName = string.Empty;
        private string _fileDetails = string.Empty;
        private string _text = string.Empty;
        private string _message =
            LocalizationManager.GetString("Preview.SelectFile");
        private ImageSource? _image;

        public FilePreviewViewModel(IFilePreviewService previewService)
        {
            _previewService = previewService
                ?? throw new ArgumentNullException(nameof(previewService));
        }

        public FilePreviewKind PreviewKind { get => _kind; private set => SetProperty(ref _kind, value); }
        public string FileName { get => _fileName; private set => SetProperty(ref _fileName, value); }
        public string FileDetails { get => _fileDetails; private set => SetProperty(ref _fileDetails, value); }
        public string Text { get => _text; private set => SetProperty(ref _text, value); }
        public string Message { get => _message; private set => SetProperty(ref _message, value); }
        public ImageSource? Image { get => _image; private set => SetProperty(ref _image, value); }

        public async Task SelectAsync(
            ISystemItem? item,
            CancellationToken cancellationToken = default)
        {
            _selectionCancellation?.Cancel();
            _selectionCancellation?.Dispose();
            _selectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            CancellationToken token = _selectionCancellation.Token;

            if(item is not IFileItem file)
            {
                Clear();
                return;
            }

            FileName = file.Name;
            FileDetails = BuildDetails(file);
            Text = string.Empty;
            Image = null;
            Message = LocalizationManager.GetString("Preview.Creating");
            PreviewKind = FilePreviewKind.Loading;

            try
            {
                FilePreviewContent content = await _previewService.LoadAsync(file, token);
                token.ThrowIfCancellationRequested();
                Text = content.Text ?? string.Empty;
                Image = content.ImageBytes is { Length: > 0 }
                    ? CreateImage(content.ImageBytes)
                    : null;
                Message = content.Message ?? string.Empty;
                PreviewKind = content.Kind;
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                if(token.IsCancellationRequested)
                    return;

                Image = null;
                Text = string.Empty;
                Message = LocalizationManager.Format(
                    "Preview.DisplayFailed",
                    Environment.NewLine,
                    ex.Message);
                PreviewKind = FilePreviewKind.Error;
            }
        }

        public void Clear()
        {
            _selectionCancellation?.Cancel();
            _selectionCancellation?.Dispose();
            _selectionCancellation = null;
            FileName = string.Empty;
            FileDetails = string.Empty;
            Text = string.Empty;
            Image = null;
            Message = LocalizationManager.GetString("Preview.SelectFile");
            PreviewKind = FilePreviewKind.Empty;
        }

        private static ImageSource CreateImage(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static string BuildDetails(IFileItem file)
        {
            string size = file.Size switch
            {
                < 1024 => LocalizationManager.Format(
                    "Preview.BytesFormat",
                    file.Size),
                < 1024 * 1024 => LocalizationManager.Format(
                    "Preview.KilobytesFormat",
                    file.Size / 1024d),
                < 1024L * 1024 * 1024 => LocalizationManager.Format(
                    "Preview.MegabytesFormat",
                    file.Size / (1024d * 1024)),
                _ => LocalizationManager.Format(
                    "Preview.GigabytesFormat",
                    file.Size / (1024d * 1024 * 1024))
            };
            return $"{file.Extension} • {size} • {file.LastWriteTimeUtc.ToLocalTime():dd.MM.yyyy HH:mm}";
        }
    }
}

using CryptoBook.Interfaces;

using CryptoBook.Infrastructure;

namespace CryptoBook.Services
{
    public sealed class ImageFilePickerService: IImageFilePicker
    {
        private readonly IFileManagerService fileManager;

        public ImageFilePickerService(IFileManagerService fileManager)
        {
            this.fileManager = fileManager
                ?? throw new ArgumentNullException(nameof(fileManager));
        }

        public Task<string?> PickImageAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationManager.GetString(
                    "File.ImagePickerTitle"),
                Filter = LocalizationManager.GetString("File.ImageFilter"),
                Multiselect = false,
                CheckFileExists = true
            };

            string? result = dialog.ShowDialog() == true
                ? fileManager.NormalizePath(dialog.FileName)
                : null;
            return Task.FromResult(result);
        }
    }
}

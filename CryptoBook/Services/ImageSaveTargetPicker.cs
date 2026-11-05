using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

public sealed class ImageSaveTargetPicker(IWindowManager windows): IImageSaveTargetPicker
{
    public string? Pick(string sourcePath, Guid ownerId)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = LocalizationManager.GetString("ImageEdit.SaveTitle"),
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP (*.bmp)|*.bmp|TIFF (*.tif;*.tiff)|*.tif;*.tiff",
            DefaultExt = ".png",
            FileName = Path.GetFileNameWithoutExtension(sourcePath) + ".png",
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(sourcePath)) ? Path.GetDirectoryName(sourcePath) : null,
            AddExtension = true,
            OverwritePrompt = true,
            CheckPathExists = true
        };
        var owner = windows.FindHostWindow(ownerId)?.Window;
        return (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true ? dialog.FileName : null;
    }
}

using CryptoBook.DTO;

using System.Windows.Media.Imaging;

namespace CryptoBook.Interfaces;

public interface IImageEditingService
{
    Task<BitmapSource> RenderAsync(BitmapSource source, ImageEditOptions options,
        int? previewMaxDimension = null, CancellationToken cancellationToken = default);
    Task SaveAsync(BitmapSource source, ImageEditOptions options, string targetPath,
        CancellationToken cancellationToken = default);
}

public interface IImageSaveTargetPicker
{
    string? Pick(string sourcePath, Guid ownerId);
}

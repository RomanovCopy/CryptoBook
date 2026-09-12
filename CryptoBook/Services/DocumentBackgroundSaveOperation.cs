using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.Services;

/// <summary>Prepares a destructive background crop on a snapshot and commits it only after the file write succeeds.</summary>
internal sealed class DocumentBackgroundSaveOperation(IFontService fonts, IMessageService messages)
{
    public async Task<bool> RunAsync(IFileTemplate template, bool isMarkdown,
        Func<FlowDocument?, Task> writeAsync, CancellationToken cancellationToken)
    {
        var editor = fonts.Service;
        var document = editor.Document;
        if(isMarkdown || template is not (XamlPackageFileTemplate or SecureFileTemplate) ||
           document.Background is not ImageBrush image || DocumentBackgroundImageLayout.IsFinalized(image))
        {
            await writeAsync(null);
            return true;
        }

        Guid dialog = await messages.ShowMessage(
            LocalizationManager.GetString("Document.BackgroundFinalizeTitle"),
            LocalizationManager.GetString("Document.BackgroundFinalizeWarning"), isCanceled: true);
        if(!messages.ShowConfirmation(dialog))
            return false;
        cancellationToken.ThrowIfCancellationRequested();
        // Do not crop a different image if the document changed while the dialog was open.
        if(editor.Document != document || document.Background != image)
            return false;

        var saved = Crop(image, document.PageWidth);
        var snapshot = new DocumentPreviewService().CreatePreview(document);
        snapshot.Background = saved;
        await writeAsync(snapshot);
        // The write callback returns only after the final file (including encryption) is committed.
        // A newer background selected during the asynchronous write belongs to an unsaved revision.
        if(editor.Document == document)
            fonts.CommitDocumentBackgroundImage(image, saved);
        return true;
    }

    internal static ImageBrush Crop(ImageBrush image, double pageWidth)
    {
        if(image.ImageSource is not BitmapSource source)
            throw new InvalidOperationException("Document background must be a bitmap.");
        var paper = image.ViewportUnits == BrushMappingMode.Absolute
            ? image.Viewport.Size : DocumentBackgroundImageLayout.GetPaperSize(pageWidth);
        Rect crop = DocumentBackgroundImageLayout.GetVisibleCrop(image, paper);
        int left = Math.Clamp((int)Math.Ceiling(crop.Left * source.PixelWidth), 0, source.PixelWidth - 1);
        int top = Math.Clamp((int)Math.Ceiling(crop.Top * source.PixelHeight), 0, source.PixelHeight - 1);
        int right = Math.Clamp((int)Math.Floor(crop.Right * source.PixelWidth), left + 1, source.PixelWidth);
        int bottom = Math.Clamp((int)Math.Floor(crop.Bottom * source.PixelHeight), top + 1, source.PixelHeight);
        var region = new Int32Rect(left, top, right - left, bottom - top);
        // Copy pixels into an independent bitmap: CroppedBitmap would retain its full Source.
        int stride = checked((region.Width * source.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[checked(stride * region.Height)];
        source.CopyPixels(region, pixels, stride, 0);
        var bitmap = BitmapSource.Create(region.Width, region.Height, source.DpiX, source.DpiY,
            source.Format, source.Palette, pixels, stride);
        bitmap.Freeze();
        var saved = new ImageBrush(bitmap)
        {
            Stretch = Stretch.Fill,
            Opacity = image.Opacity,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(paper)
        };
        saved.SetValue(DocumentBackgroundImageLayout.IsFinalizedProperty, true);
        saved.Freeze();
        return saved;
    }
}

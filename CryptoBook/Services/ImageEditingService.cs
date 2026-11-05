using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.Services;

public sealed class ImageEditingService: IImageEditingService
{
    public const int MaxDimension = 32768;
    public const long MaxPixels = 100_000_000;

    public static bool IsValidSize(int width, int height) =>
        width is > 0 and <= MaxDimension && height is > 0 and <= MaxDimension &&
        (long)width * height <= MaxPixels;

    public Task<BitmapSource> RenderAsync(BitmapSource source, ImageEditOptions options,
        int? previewMaxDimension = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!source.IsFrozen)
            throw new ArgumentException("The source bitmap must be frozen.", nameof(source));
        Validate(options);
        if(previewMaxDimension is <= 0)
            throw new ArgumentOutOfRangeException(nameof(previewMaxDimension));
        return Task.Run(() => Render(source, options, previewMaxDimension, cancellationToken), cancellationToken);
    }

    public async Task SaveAsync(BitmapSource source, ImageEditOptions options, string targetPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        // Validate the format before allocating the full-size result or touching files.
        string extension = Path.GetExtension(targetPath).ToLowerInvariant();
        if(extension is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff"))
            throw new NotSupportedException(LocalizationManager.GetString("ImageEdit.UnsupportedFormat"));
        BitmapSource result = await RenderAsync(source, options, cancellationToken: cancellationToken);
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            BitmapEncoder encoder = extension switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                ".bmp" => new BmpBitmapEncoder(),
                ".tif" or ".tiff" => new TiffBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };
            if(extension is ".jpg" or ".jpeg" or ".bmp")
                result = FlattenTransparency(result, cancellationToken);
            encoder.Frames.Add(BitmapFrame.Create(result));
            string fullPath = Path.GetFullPath(targetPath);
            string temporaryPath = Path.Combine(Path.GetDirectoryName(fullPath)!, $".image-{Guid.NewGuid():N}.tmp");
            try
            {
                using(var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    encoder.Save(stream);
                    stream.Flush(flushToDisk: true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                AtomicFileCommit.CommitWithoutBackup(temporaryPath, fullPath);
            }
            finally
            {
                if(File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }, cancellationToken);
    }

    private static BitmapSource Render(BitmapSource source, ImageEditOptions options,
        int? previewMaxDimension, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        double ratio = previewMaxDimension is int limit
            ? Math.Min(1, (double)limit / Math.Max(options.Width, options.Height)) : 1;
        int width = Math.Max(1, (int)Math.Round(options.Width * ratio));
        int height = Math.Max(1, (int)Math.Round(options.Height * ratio));
        BitmapSource scaled = source;
        if(width != source.PixelWidth || height != source.PixelHeight)
            scaled = new TransformedBitmap(source,
                new ScaleTransform((double)width / source.PixelWidth, (double)height / source.PixelHeight));
        var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        converted.CopyPixels(pixels, stride, 0);
        double contrast = 1 + options.Contrast / 100;
        double brightness = options.Brightness * 255 / 100;
        double saturation = 1 + options.Saturation / 100;
        for(int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            for(int x = y * stride; x < (y + 1) * stride; x += 4)
            {
                double b = pixels[x], g = pixels[x + 1], r = pixels[x + 2];
                double gray = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                pixels[x] = Adjust(gray + (b - gray) * saturation, contrast, brightness);
                pixels[x + 1] = Adjust(gray + (g - gray) * saturation, contrast, brightness);
                pixels[x + 2] = Adjust(gray + (r - gray) * saturation, contrast, brightness);
            }
        }
        var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    private static byte Adjust(double value, double contrast, double brightness) =>
        (byte)Math.Clamp(Math.Round((value - 127.5) * contrast + 127.5 + brightness), 0, 255);

    private static BitmapSource FlattenTransparency(BitmapSource source, CancellationToken token)
    {
        int stride = checked(source.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);
        for(int y = 0; y < source.PixelHeight; y++)
        {
            token.ThrowIfCancellationRequested();
            for(int x = y * stride; x < (y + 1) * stride; x += 4)
            {
                int alpha = pixels[x + 3];
                for(int channel = 0; channel < 3; channel++)
                    pixels[x + channel] = (byte)((pixels[x + channel] * alpha + 255 * (255 - alpha) + 127) / 255);
                pixels[x + 3] = 255;
            }
        }
        var opaque = BitmapSource.Create(source.PixelWidth, source.PixelHeight,
            source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        var result = new FormatConvertedBitmap(opaque, PixelFormats.Bgr24, null, 0);
        result.Freeze();
        return result;
    }

    private static void Validate(ImageEditOptions options)
    {
        if(!IsValidSize(options.Width, options.Height))
            throw new ArgumentOutOfRangeException(nameof(options), LocalizationManager.GetString("ImageEdit.InvalidSize"));
        if(!double.IsFinite(options.Contrast) || options.Contrast is < -100 or > 100 ||
           !double.IsFinite(options.Brightness) || options.Brightness is < -100 or > 100 ||
           !double.IsFinite(options.Saturation) || options.Saturation is < -100 or > 100)
            throw new ArgumentOutOfRangeException(nameof(options));
    }
}

using CryptoBook.DTO;
using CryptoBook.Services;

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ImageEditingServiceTests
{
    [StaFact]
    public async Task NeutralSettingsPreservePixelsAndTransparency()
    {
        BitmapSource source = Bitmap(2, 1, [10, 80, 200, 117, 240, 40, 30, 255]);
        BitmapSource result = await new ImageEditingService().RenderAsync(source, new(2, 1));
        Assert.Equal(Pixels(source), Pixels(result));
        Assert.True(result.IsFrozen);
    }

    [StaTheory]
    [InlineData(100, 255)]
    [InlineData(-100, 0)]
    public async Task BrightnessClampsChannelsAndPreservesAlpha(double brightness, byte expected)
    {
        var source = Bitmap(1, 1, [10, 80, 200, 117]);
        var result = await new ImageEditingService().RenderAsync(source, new(1, 1, Brightness: brightness));
        Assert.Equal(new byte[] { expected, expected, expected, 117 }, Pixels(result));
        Assert.Equal(new byte[] { 10, 80, 200, 117 }, Pixels(source));
    }

    [StaFact]
    public async Task DesaturationProducesLuminanceAndContrastHasVisibleEffect()
    {
        var service = new ImageEditingService();
        var source = Bitmap(1, 1, [10, 80, 200, 117]);
        var gray = Pixels(await service.RenderAsync(source, new(1, 1, Saturation: -100)));
        Assert.Equal(gray[0], gray[1]);
        Assert.Equal(gray[1], gray[2]);
        var flat = Pixels(await service.RenderAsync(source, new(1, 1, Contrast: -100)));
        Assert.Equal(new byte[] { 128, 128, 128, 117 }, flat);
        var contrast = Pixels(await service.RenderAsync(source, new(1, 1, Contrast: 100)));
        Assert.True(contrast[0] < 10);
        Assert.True(contrast[2] > 200);
        var saturated = Pixels(await service.RenderAsync(source, new(1, 1, Saturation: 100)));
        Assert.True(saturated[0] < 10);
        Assert.True(saturated[2] > 200);
    }

    [StaTheory]
    [InlineData(7, 3)]
    [InlineData(1, 13)]
    [InlineData(31, 1)]
    public async Task ResizeUsesExactRequestedPixelDimensions(int width, int height)
    {
        var result = await new ImageEditingService().RenderAsync(Bitmap(4, 2), new(width, height));
        Assert.Equal(width, result.PixelWidth);
        Assert.Equal(height, result.PixelHeight);
    }

    [StaFact]
    public async Task PreviewIsBoundedAndUsesRequestedAspectRatio()
    {
        var result = await new ImageEditingService().RenderAsync(Bitmap(4, 2), new(4000, 1000), 200);
        Assert.Equal(200, result.PixelWidth);
        Assert.Equal(50, result.PixelHeight);
    }

    [StaTheory]
    [InlineData(".png", typeof(PngBitmapDecoder))]
    [InlineData(".jpg", typeof(JpegBitmapDecoder))]
    [InlineData(".jpeg", typeof(JpegBitmapDecoder))]
    [InlineData(".bmp", typeof(BmpBitmapDecoder))]
    [InlineData(".tif", typeof(TiffBitmapDecoder))]
    [InlineData(".tiff", typeof(TiffBitmapDecoder))]
    public async Task SavedFormatsDecodeWithNewDimensions(string extension, Type decoderType)
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "edited" + extension);
        await new ImageEditingService().SaveAsync(Bitmap(4, 2), new(7, 3, Brightness: 10), path);
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.IsType(decoderType, decoder);
        var frame = Assert.Single(decoder.Frames);
        Assert.Equal(7, frame.PixelWidth);
        Assert.Equal(3, frame.PixelHeight);
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    [StaTheory]
    [InlineData(".png")]
    [InlineData(".tiff")]
    public async Task LosslessFormatsPreserveTransparencyAndJpegUsesWhiteBackground(string extension)
    {
        using var directory = new TestDirectory();
        var service = new ImageEditingService();
        var transparent = Bitmap(1, 1, [0, 0, 0, 0]);
        string png = Path.Combine(directory.Path, "image" + extension), jpg = Path.Combine(directory.Path, "image.jpg");
        await service.SaveAsync(transparent, new(1, 1), png);
        await service.SaveAsync(transparent, new(1, 1), jpg);
        Assert.Equal(0, Pixels(Read(png))[3]);
        Assert.All(Pixels(Read(jpg)), value => Assert.InRange(value, (byte)254, (byte)255));
    }

    [StaFact]
    public async Task OverwriteAndReloadDoNotUseTheOldImageCache()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "image.png");
        var service = new ImageEditingService();
        var viewer = new ImageService();
        await service.SaveAsync(Bitmap(4, 2), new(4, 2), path);
        await viewer.LoadImageAsync(path);
        await service.SaveAsync(Bitmap(4, 2), new(7, 3, Brightness: 100), path);
        await viewer.LoadImageAsync(path);
        Assert.Equal(7, Assert.IsAssignableFrom<BitmapSource>(viewer.ImageSource).PixelWidth);
        Assert.Equal(255, Pixels((BitmapSource)viewer.ImageSource!)[0]);
        viewer.Clear();
    }

    [StaFact]
    public async Task BlockedReplacementPreservesOriginalAndCleansStagedFile()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "image.png");
        byte[] original = [1, 2, 3, 4];
        File.WriteAllBytes(path, original);
        using(var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => new ImageEditingService().SaveAsync(Bitmap(4, 2), new(7, 3), path));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    [StaFact]
    public async Task CancellationAndUnsupportedFormatDoNotChangeOriginal()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "original.png");
        File.WriteAllBytes(path, [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new ImageEditingService();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync(Bitmap(4, 2), new(4, 2), path, cancellation.Token));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        string protectedPath = Path.Combine(directory.Path, "original.cbook");
        File.WriteAllBytes(protectedPath, [4, 5, 6]);
        await Assert.ThrowsAsync<NotSupportedException>(() => service.SaveAsync(Bitmap(4, 2), new(4, 2), protectedPath));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(protectedPath));
        Assert.Equal(2, Directory.GetFiles(directory.Path).Length);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, -1, false)]
    [InlineData(32769, 1, false)]
    [InlineData(32768, 32768, false)]
    [InlineData(10000, 10000, true)]
    [InlineData(1, 32768, true)]
    public void InvalidOrExcessiveDimensionsAreRejected(int width, int height, bool valid) =>
        Assert.Equal(valid, ImageEditingService.IsValidSize(width, height));

    internal static BitmapSource Bitmap(int width, int height, byte[]? pixels = null)
    {
        pixels ??= Enumerable.Repeat(new byte[] { 40, 90, 170, 255 }, width * height).SelectMany(p => p).ToArray();
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        result.Freeze();
        return result;
    }

    internal static BitmapSource Read(string path)
    {
        using var stream = File.OpenRead(path);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return frame;
    }

    internal static byte[] Pixels(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        converted.CopyPixels(pixels, source.PixelWidth * 4, 0);
        return pixels;
    }

    internal sealed class TestDirectory: IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CryptoBook.ImageEdit." + Guid.NewGuid().ToString("N"));
        public TestDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

using CryptoBook.Accessors;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentBackgroundSaveTests
{
    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_ContainsOnlyCropAndLocksOnlyAfterSuccessfulWrite(bool secure)
    {
        var (editor, fonts) = Create();
        var original = (ImageBrush)editor.Document.Background;
        Assert.Equal(new Rect(0.25, 0, 0.5, 1), DocumentBackgroundImageLayout.GetVisibleCrop(original, original.Viewport.Size));
        Assert.Equal(200, ((BitmapSource)DocumentBackgroundSaveOperation.Crop(original, editor.Document.PageWidth).ImageSource).PixelWidth);
        var messages = new Messages(true);
        var operation = new DocumentBackgroundSaveOperation(fonts, messages);
        var dispatcher = new WpfDispatcherService(Dispatcher.CurrentDispatcher);
        var handler = new XamlPackageDocumentFormatHandler(dispatcher);
        var saver = new FlowDocumentSaveService(dispatcher, new DocumentFormatHandlerRegistry([handler]));
        IFileTemplate template = secure ? new SecureFileTemplate() : new XamlPackageFileTemplate();
        byte[]? content = null;
        string path = Path.Combine(Path.GetTempPath(), "background-" + Guid.NewGuid() + ".XamlPackage");
        try
        {
            Assert.True(await operation.RunAsync(template, false, async snapshot =>
            {
                Assert.NotNull(snapshot);
                Assert.Same(original, editor.Document.Background);
                Assert.Equal(1, messages.Count);
                if(secure)
                {
                    using var stream = new MemoryStream();
                    await saver.SaveToStreamAsync(editor, stream, new XamlPackageFileTemplate(), documentSnapshot: snapshot);
                    content = stream.ToArray();
                }
                else
                {
                    await saver.SaveToFileAsync(editor, path, template, documentSnapshot: snapshot);
                    content = await File.ReadAllBytesAsync(path);
                }
                Assert.Same(original, editor.Document.Background);
            }, default));

            var saved = Assert.IsType<ImageBrush>(editor.Document.Background);
            Assert.True(DocumentBackgroundImageLayout.IsFinalized(saved));
            Assert.Equal(new Rect(0, 0, 1, 1), saved.Viewbox);
            Assert.Equal(original.Viewport, saved.Viewport);
            Assert.Equal(Render(original), Render(saved));
            fonts.SetDocumentBackgroundImageCrop(new Rect(0, 0, 0.5, 1));
            Assert.Same(saved, editor.Document.Background);
            using var archive = new ZipArchive(new MemoryStream(content!));
            var entry = Assert.Single(archive.Entries, e => e.FullName.EndsWith(".png"));
            using var stream = entry.Open();
            using var imageBytes = new MemoryStream();
            stream.CopyTo(imageBytes);
            imageBytes.Position = 0;
            var bitmap = BitmapDecoder.Create(imageBytes, BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(200, bitmap.PixelWidth);
            Assert.Equal(400, bitmap.PixelHeight);
            var pixels = new byte[200 * 400 * 4];
            new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 800, 0);
            for(int i = 0; i < pixels.Length; i += 4)
            {
                Assert.Equal(255, pixels[i + 1]);
                Assert.Equal(0, pixels[i + 2]); // Excluded red pixels cannot be recovered from the file.
            }
            var loaded = new FlowDocument();
            await handler.LoadAsync(loaded, content!);
            Assert.True(DocumentBackgroundImageLayout.IsFinalized((ImageBrush)loaded.Background));
            Assert.Equal(saved.Viewport, ((ImageBrush)loaded.Background).Viewport);
            Assert.True(await operation.RunAsync(template, false, snapshot =>
            {
                Assert.Null(snapshot);
                return Task.CompletedTask;
            }, default));
            Assert.Equal(1, messages.Count);

            fonts.SetDocumentBackgroundImage((BitmapSource)original.ImageSource);
            Assert.False(DocumentBackgroundImageLayout.IsFinalized((ImageBrush)editor.Document.Background));
            Assert.True(await operation.RunAsync(template, false, _ => Task.CompletedTask, default));
            Assert.Equal(2, messages.Count);
        }
        finally
        {
            if(File.Exists(path)) File.Delete(path);
        }
    }

    [WpfTheory]
    [InlineData("decline")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task UnsuccessfulSave_PreservesOriginalAndAllowsRepositioning(string outcome)
    {
        var (editor, fonts) = Create();
        var original = (ImageBrush)editor.Document.Background;
        var operation = new DocumentBackgroundSaveOperation(fonts, new Messages(outcome != "decline"));
        bool written = false;
        Task<bool> Save() => operation.RunAsync(new XamlPackageFileTemplate(), false, _ =>
        {
            written = true;
            if(outcome == "cancel") throw new OperationCanceledException();
            throw new IOException("Simulated disk failure");
        }, default);
        if(outcome == "decline")
        {
            Assert.False(await Save());
            Assert.False(written);
        }
        else if(outcome == "cancel")
            await Assert.ThrowsAsync<OperationCanceledException>(Save);
        else
            await Assert.ThrowsAsync<IOException>(Save);
        Assert.Same(original, editor.Document.Background);
        fonts.SetDocumentBackgroundImageCrop(new Rect(0, 0, 0.5, 1));
        Assert.NotSame(original, editor.Document.Background);
    }

    [WpfFact]
    public async Task NewBackgroundDuringSave_IsNotOverwrittenOrLocked()
    {
        var (editor, fonts) = Create();
        ImageBrush? newer = null;
        var operation = new DocumentBackgroundSaveOperation(fonts, new Messages(true));
        Assert.True(await operation.RunAsync(new XamlPackageFileTemplate(), false, snapshot =>
        {
            Assert.NotNull(snapshot);
            fonts.SetDocumentBackgroundImageCrop(new Rect(0, 0, 0.5, 1));
            newer = (ImageBrush)editor.Document.Background;
            return Task.CompletedTask;
        }, default));
        Assert.Same(newer, editor.Document.Background);
        Assert.False(DocumentBackgroundImageLayout.IsFinalized(newer!));
    }

    [WpfFact]
    public async Task RecoverySerialization_KeepsOriginalEditableWithoutFinalization()
    {
        var (editor, _) = Create();
        var dispatcher = new WpfDispatcherService(Dispatcher.CurrentDispatcher);
        var handler = new XamlPackageDocumentFormatHandler(dispatcher);
        var saver = new FlowDocumentSaveService(dispatcher, new DocumentFormatHandlerRegistry([handler]));
        using var stream = new MemoryStream();
        await saver.SaveToStreamAsync(editor, stream, new XamlPackageFileTemplate());
        var restored = new FlowDocument();
        await handler.LoadAsync(restored, stream.ToArray());
        var image = (ImageBrush)restored.Background;
        Assert.Equal(400, ((BitmapSource)image.ImageSource).PixelWidth);
        Assert.False(DocumentBackgroundImageLayout.IsFinalized(image));
        Assert.Equal(((ImageBrush)editor.Document.Background).Viewbox, image.Viewbox);
    }

    private static (IRichTextBoxService, FontService) Create()
    {
        var factory = new Paragraphs();
        IRichTextBoxService editor = new RichTextBoxService(factory, new Navigation(), new DocumentAppearanceDefaults());
        var fonts = new FontService(editor, new InlineService(editor, new ReflectionPropertyAccessor(), factory),
            new Preferences(), new DocumentAppearanceDefaults());
        var pixels = new byte[400 * 400 * 4];
        for(int y = 0; y < 400; y++)
            for(int x = 0; x < 400; x++)
            {
                int i = (y * 400 + x) * 4;
                pixels[i + (x is >= 100 and < 300 ? 1 : 2)] = 255;
                pixels[i + 3] = 255;
            }
        var bitmap = BitmapSource.Create(400, 400, 96, 96, PixelFormats.Bgra32, null, pixels, 1600);
        bitmap.Freeze();
        editor.Document.Background = new ImageBrush(bitmap)
        {
            Stretch = Stretch.UniformToFill,
            Viewbox = new Rect(0.25, 0, 0.5, 1),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 200, 400)
        };
        return (editor, fonts);
    }

    private static byte[] Render(ImageBrush brush)
    {
        var visual = new DrawingVisual();
        using(var drawing = visual.RenderOpen())
            drawing.DrawRectangle(brush, null, brush.Viewport);
        var bitmap = new RenderTargetBitmap(200, 400, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[200 * 400 * 4];
        bitmap.CopyPixels(pixels, 800, 0);
        return pixels;
    }

    private sealed class Messages(bool accepted) : IMessageService
    {
        public int Count { get; private set; }
        public Task<Guid> ShowMessage(string title, string message, bool isCanceled = false)
        {
            Assert.True(isCanceled);
            Assert.NotEmpty(message);
            Count++;
            return Task.FromResult(Guid.NewGuid());
        }
        public void CloseDialog(Guid id) { }
        public bool ShowConfirmation(Guid id) => accepted;
    }
    private sealed class Paragraphs : IParagraphFactory
    {
        public IParagraphService Create(Inline? inline = null)
        {
            var paragraph = new ParagraphService();
            if(inline is not null) paragraph.Inlines.Add(inline);
            return paragraph;
        }
    }
    private sealed class Navigation : IUriNavigationService { public bool TryOpen(Uri uri) => false; }
    private sealed class Preferences : IDocumentBackgroundPreferenceStore
    {
        public System.Drawing.Color? Load() => null;
        public void Save(System.Drawing.Color color) { }
    }
}

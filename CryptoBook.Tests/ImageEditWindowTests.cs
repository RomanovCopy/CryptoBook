using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ImageEditWindowTests
{
    [WpfFact]
    public async Task CompiledEditorBindsAndSavesInBothThemesAndPlayerOpensSavedResult()
    {
        using var directory = new ImageEditingServiceTests.TestDirectory();
        var app = System.Windows.Application.Current ?? new System.Windows.Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Resources["Res"] = new ResourceWrapper();
        foreach(string resource in new[] { "WindowStyles", "ControlStyles", "ButtonStyles", "ComboBoxStyles", "ThemedControls" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/CryptoBook;component/Styles/{resource}.xaml", UriKind.Relative)
            });
        var picker = new ImageEditViewModelTests.FakePicker();
        using var container = new Startup().ConfigureServices(app, builder =>
        {
            builder.RegisterInstance<IImageSaveTargetPicker>(picker);
            builder.RegisterInstance<IPinnedDocumentStore>(new JsonPinnedDocumentStore(Path.Combine(directory.Path, "pinned.json")));
            builder.RegisterInstance<IRecentDocumentStore>(new JsonRecentDocumentStore(Path.Combine(directory.Path, "recent.json")));
        });
        var windows = container.Resolve<IWindowManager>();
        var listener = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        var previewCancellations = new ConcurrentQueue<Exception>();
        void RecordPreviewCancellation(object? sender, FirstChanceExceptionEventArgs args)
        {
            if(args.Exception is OperationCanceledException && args.Exception.StackTrace is string stack &&
               (stack.Contains(nameof(ImageEditViewModel), StringComparison.Ordinal) ||
                stack.Contains(nameof(ImageEditingService), StringComparison.Ordinal)))
                previewCancellations.Enqueue(args.Exception);
        }
        AppDomain.CurrentDomain.FirstChanceException += RecordPreviewCancellation;
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            BitmapSource source = SampleImage();
            foreach(string theme in new[] { "Light", "Dark" })
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"/CryptoBook;component/Themes/{theme}Theme.xaml", UriKind.Relative)
                });
                foreach(string culture in new[] { "ru-RU", "en-US", "uk-UA", "de-DE" })
                {
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                    ResourceWrapper.NotifyCultureChanged();
                    string sourcePath = Path.Combine(directory.Path, "original.png");
                    picker.Target = Path.Combine(directory.Path, $"{theme}-{culture}.png");
                    Guid id = windows.CreateWindow<ImageEditWindow>(new Dictionary<string, object?>
                    {
                        [ImageEditViewModel.RequestContextKey] = new ImageEditRequest(source, sourcePath, culture == "de-DE")
                    });
                    var window = Assert.IsType<ImageEditWindow>(windows.FindHostWindow(id)!.Window);
                    var vm = Assert.IsType<ImageEditViewModel>(window.DataContext);
                    window.Show();
                    try
                    {
                        Assert.DoesNotContain("ImageEdit.", window.Title);
                        foreach(var (name, final) in new[]
                            { ("ContrastSlider", 20), ("BrightnessSlider", 10), ("SaturationSlider", -50) })
                        {
                            var slider = Assert.IsType<Slider>(window.FindName(name));
                            foreach(int value in new[] { -100, 100, final })
                            {
                                slider.Value = value;
                                await Task.Delay(40);
                            }
                        }
                        Assert.Equal(20, vm.Contrast);
                        Assert.Equal(10, vm.Brightness);
                        Assert.Equal(-50, vm.Saturation);
                        var width = Assert.IsType<TextBox>(window.FindName("WidthBox"));
                        var height = Assert.IsType<TextBox>(window.FindName("HeightBox"));
                        width.Text = "320";
                        Assert.Equal("180", height.Text);
                        height.Text = "90";
                        Assert.Equal("160", width.Text);
                        var keepAspectRatio = Assert.IsType<CheckBox>(window.FindName("KeepAspectRatioBox"));
                        keepAspectRatio.IsChecked = false;
                        height.Text = "240";
                        Assert.Equal("160", width.Text);
                        keepAspectRatio.IsChecked = true;
                        Assert.Equal("90", height.Text);
                        keepAspectRatio.IsChecked = false;
                        foreach(var field in new[] { width, height })
                        {
                            string valid = field.Text;
                            foreach(string invalid in new[] { "", "0", "-1", "abc", "32769", "2147483648" })
                            {
                                field.Text = invalid;
                                Assert.False(Assert.IsType<Button>(window.FindName("SaveButton")).IsEnabled);
                                Assert.NotEmpty(vm.ValidationMessage);
                            }
                            field.Text = valid;
                        }
                        var reset = Assert.IsType<Button>(window.FindName("ResetButton"));
                        Assert.True(reset.IsEnabled);
                        reset.Command.Execute(reset.CommandParameter);
                        Assert.Equal("640", width.Text);
                        Assert.Equal("360", height.Text);
                        foreach(string name in new[] { "ContrastSlider", "BrightnessSlider", "SaturationSlider" })
                            Assert.Equal(0, Assert.IsType<Slider>(window.FindName(name)).Value);
                        await vm.RefreshPreviewAsync();
                        Assert.Equal(ImageEditingServiceTests.Pixels(source), ImageEditingServiceTests.Pixels(vm.Preview));
                        Assert.IsType<Slider>(window.FindName("ContrastSlider")).Value = 20;
                        Assert.IsType<Slider>(window.FindName("BrightnessSlider")).Value = 10;
                        Assert.IsType<Slider>(window.FindName("SaturationSlider")).Value = -50;
                        width.Text = "320";
                        height.Text = "240";
                        await vm.RefreshPreviewAsync();
                        Assert.Equal(320, vm.Preview.PixelWidth);
                        Assert.Equal(240, vm.Preview.PixelHeight);
                        window.UpdateLayout();
                        if(culture == "ru-RU") Render(window, $"{theme}-{culture}-default");
                        window.Width = window.MinWidth;
                        window.Height = window.MinHeight;
                        window.UpdateLayout();
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        var save = Assert.IsType<Button>(window.FindName("SaveButton"));
                        Assert.True(save.IsEnabled);
                        var bounds = save.TransformToAncestor(window).TransformBounds(new Rect(save.RenderSize));
                        Assert.True(bounds.Right <= window.ActualWidth && bounds.Bottom <= window.ActualHeight);
                        if(vm.IsEncrypted)
                        {
                            var hint = Assert.IsType<TextBlock>(window.FindName("EncryptedHint"));
                            Assert.True(hint.IsVisible);
                            var hintBounds = hint.TransformToAncestor(window).TransformBounds(new Rect(hint.RenderSize));
                            Assert.True(hintBounds.Bottom <= bounds.Top);
                        }
                        Render(window, $"{theme}-{culture}");
                        string? target = picker.Target;
                        picker.Target = null;
                        await vm.SaveCommand.ExecuteAsync();
                        Assert.True(window.IsVisible);
                        Assert.False(vm.HasResult);
                        Assert.True(vm.CanEdit);
                        Assert.False(File.Exists(sourcePath));
                        picker.Target = target;
                        await vm.SaveCommand.ExecuteAsync();
                        Assert.False(window.IsVisible);
                        Assert.Equal(picker.Target, windows.GetResult<string>(id));
                        Assert.Equal(320, ImageEditingServiceTests.Read(picker.Target!).PixelWidth);
                        Assert.Equal(ImageEditingServiceTests.Pixels(vm.Preview),
                            ImageEditingServiceTests.Pixels(ImageEditingServiceTests.Read(picker.Target!)));
                        Assert.False(File.Exists(sourcePath));
                    }
                    finally { window.Close(); }
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                }
            }
            await VerifyPlayerCommandAsync(windows, picker, directory.Path, source);
            Assert.Empty(listener.Errors);
            Assert.Empty(previewCancellations);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
            ResourceWrapper.NotifyCultureChanged();
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            AppDomain.CurrentDomain.FirstChanceException -= RecordPreviewCancellation;
        }
    }

    private static async Task VerifyPlayerCommandAsync(IWindowManager windows,
        ImageEditViewModelTests.FakePicker picker, string directory, BitmapSource source)
    {
        string original = Path.Combine(directory, "player.png");
        await new ImageEditingService().SaveAsync(source, new(source.PixelWidth, source.PixelHeight), original);
        byte[] originalBytes = File.ReadAllBytes(original);
        Guid id = windows.CreateWindow<CryptoBook.Views.MediaPlayer>(new Dictionary<string, object?> { ["path"] = original });
        var window = windows.FindHostWindow(id)!.Window;
        var vm = Assert.IsAssignableFrom<IMediaPlayerViewModel>(window.DataContext);
        window.Show();
        try
        {
            await WaitUntilAsync(() => vm.EditImageCommand.CanExecute(null));
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = appDispatcher().BeginInvoke(new Action(() =>
            {
                try
                {
                    var editor = Assert.Single(System.Windows.Application.Current.Windows.OfType<ImageEditWindow>());
                    var editorVm = Assert.IsType<ImageEditViewModel>(editor.DataContext);
                    editorVm.Brightness = 50;
                    editorVm.Width = "160";
                    var cancel = Assert.IsType<Button>(editor.FindName("CancelButton"));
                    Assert.True(cancel.IsEnabled);
                    cancel.Command.Execute(cancel.CommandParameter);
                    Assert.False(editor.IsVisible);
                    Assert.False(editorVm.HasResult);
                    cancelled.TrySetResult();
                }
                catch(Exception exception)
                {
                    foreach(var editor in System.Windows.Application.Current.Windows.OfType<ImageEditWindow>().ToArray()) editor.Close();
                    cancelled.TrySetException(exception);
                }
            }));
            vm.EditImageCommand.Execute(null);
            await cancelled.Task;
            Assert.Equal(original, vm.ImageService.CurrentImagePath);
            Assert.Equal(originalBytes, File.ReadAllBytes(original));
            Assert.Equal(source.PixelWidth, Assert.IsAssignableFrom<BitmapSource>(vm.ImageService.ImageSource).PixelWidth);
            await WaitUntilAsync(() => vm.EditImageCommand.CanExecute(null));
            picker.Target = Path.Combine(directory, "player-edited.png");
            var edited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = appDispatcher().BeginInvoke(new Action(async () =>
            {
                try
                {
                    var editor = Assert.Single(System.Windows.Application.Current.Windows.OfType<ImageEditWindow>());
                    Assert.Same(window, editor.Owner);
                    var editorVm = Assert.IsType<ImageEditViewModel>(editor.DataContext);
                    editorVm.Width = "160";
                    editorVm.Brightness = 25;
                    await editorVm.SaveCommand.ExecuteAsync();
                    edited.TrySetResult();
                }
                catch(Exception exception)
                {
                    foreach(var editor in System.Windows.Application.Current.Windows.OfType<ImageEditWindow>().ToArray()) editor.Close();
                    edited.TrySetException(exception);
                }
            }));
            vm.EditImageCommand.Execute(null);
            await edited.Task;
            await WaitUntilAsync(() => vm.ImageService.CurrentImagePath == picker.Target &&
                !vm.ImageService.IsLoading && vm.EditImageCommand.CanExecute(null));
            Assert.Equal(160, Assert.IsAssignableFrom<BitmapSource>(vm.ImageService.ImageSource).PixelWidth);
            Assert.Equal(originalBytes, File.ReadAllBytes(original));

            // Saving to the same path must also refresh the existing player image.
            picker.Target = vm.ImageService.CurrentImagePath;
            var overwritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = appDispatcher().BeginInvoke(new Action(async () =>
            {
                try
                {
                    var editor = Assert.Single(System.Windows.Application.Current.Windows.OfType<ImageEditWindow>());
                    var editorVm = Assert.IsType<ImageEditViewModel>(editor.DataContext);
                    editorVm.Width = "80";
                    await editorVm.SaveCommand.ExecuteAsync();
                    overwritten.TrySetResult();
                }
                catch(Exception exception)
                {
                    foreach(var editor in System.Windows.Application.Current.Windows.OfType<ImageEditWindow>().ToArray()) editor.Close();
                    overwritten.TrySetException(exception);
                }
            }));
            vm.EditImageCommand.Execute(null);
            await overwritten.Task;
            await WaitUntilAsync(() => vm.ImageService.ImageSource is BitmapSource bitmap && bitmap.PixelWidth == 80);
        }
        finally { window.Close(); }

        static Dispatcher appDispatcher() => System.Windows.Application.Current.Dispatcher;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while(!condition())
        {
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), "Timed out waiting for the player.");
            await Task.Delay(20);
        }
    }

    private static BitmapSource SampleImage()
    {
        byte[] pixels = new byte[640 * 360 * 4];
        for(int y = 0; y < 360; y++)
            for(int x = 0; x < 640; x++)
            {
                int i = (y * 640 + x) * 4;
                pixels[i] = (byte)(x * 255 / 640);
                pixels[i + 1] = (byte)(y * 255 / 360);
                pixels[i + 2] = (byte)(255 - x * 255 / 640);
                pixels[i + 3] = 255;
            }
        return ImageEditingServiceTests.Bitmap(640, 360, pixels);
    }

    private static void Render(Window window, string name)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while(root.Parent is not null && !Directory.Exists(Path.Combine(root.FullName, "CryptoBook.Tests")))
            root = root.Parent;
        string folder = Path.Combine(root.FullName, "artifacts", "ImageEditing", "screenshots");
        Directory.CreateDirectory(folder);
        using var output = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(output);
    }

    private sealed class BindingErrors: TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if(!string.IsNullOrEmpty(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}

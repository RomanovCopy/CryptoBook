using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.ComponentModel;
using System.Reflection;
using System.Windows.Media.Imaging;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ImageEditViewModelTests
{
    [WpfFact]
    public void AspectRatioWorksFromBothDimensionsAndCanBeDisabled()
    {
        using var fixture = new Fixture();
        var vm = fixture.ViewModel;
        Assert.True(vm.KeepAspectRatio);
        vm.Width = "200";
        Assert.Equal("100", vm.Height);
        vm.Height = "25";
        Assert.Equal("50", vm.Width);
        vm.KeepAspectRatio = false;
        vm.Width = "75";
        Assert.Equal("25", vm.Height);
        vm.KeepAspectRatio = true;
        Assert.Equal("38", vm.Height);
        vm.Height = "1";
        Assert.Equal("2", vm.Width);
    }

    [WpfFact]
    public async Task InvalidInputDisablesSaveAndResetRestoresOriginal()
    {
        using var fixture = new Fixture();
        var vm = fixture.ViewModel;
        foreach(string invalid in new[] { "", "0", "-1", "abc", "32769", "2147483648" })
        {
            vm.Width = invalid;
            Assert.False(vm.SaveCommand.CanExecute(null));
            Assert.NotEmpty(vm.ValidationMessage);
        }
        vm.Width = "200";
        vm.Brightness = 30;
        vm.Contrast = 25;
        vm.Saturation = -20;
        vm.ResetCommand.Execute(null);
        Assert.Equal("4", vm.Width);
        Assert.Equal("2", vm.Height);
        Assert.Equal(0, vm.Contrast);
        Assert.Equal(0, vm.Brightness);
        Assert.Equal(0, vm.Saturation);
        Assert.True(vm.SaveCommand.CanExecute(null));
        await vm.RefreshPreviewAsync();
        Assert.Equal(ImageEditingServiceTests.Pixels(fixture.Source), ImageEditingServiceTests.Pixels(vm.Preview));
    }

    [WpfFact]
    public async Task CancelledPickerDoesNotWriteAndSuccessfulSaveReturnsChosenPath()
    {
        using var fixture = new Fixture();
        var vm = fixture.ViewModel;
        await vm.SaveCommand.ExecuteAsync();
        Assert.Equal(0, fixture.Service.Saves);
        Assert.False(vm.HasResult);
        fixture.Picker.Target = "edited.png";
        vm.Width = "10";
        vm.Brightness = 20;
        await vm.SaveCommand.ExecuteAsync();
        Assert.Equal("edited.png", vm.Result);
        Assert.Equal(new ImageEditOptions(10, 5, Brightness: 20), fixture.Service.SavedOptions);
        Assert.Equal(1, fixture.Windows.CloseCount);
    }

    [WpfFact]
    public async Task ClosingDuringSaveCancelsAndWaitsForTheOperation()
    {
        using var fixture = new Fixture();
        fixture.Picker.Target = "edited.png";
        fixture.Service.WaitForCancellation = true;
        var vm = fixture.ViewModel;
        Task saving = vm.SaveCommand.ExecuteAsync();
        Assert.True(vm.IsSaving);
        Assert.False(vm.CanEdit);
        Assert.False(vm.SaveCommand.CanExecute(null));
        var closing = new CancelEventArgs();
        vm.Closing.Execute(closing);
        Assert.True(closing.Cancel);
        Assert.Equal(0, fixture.Windows.CloseCount);
        await saving;
        Assert.False(vm.IsSaving);
        Assert.False(vm.HasResult);
        Assert.Equal(1, fixture.Windows.CloseCount);
    }

    [WpfFact]
    public async Task FailureKeepsTheEditorOpenAndAllowsRetry()
    {
        using var fixture = new Fixture();
        fixture.Picker.Target = "edited.png";
        fixture.Service.FailSave = true;
        await fixture.ViewModel.SaveCommand.ExecuteAsync();
        Assert.False(fixture.ViewModel.HasResult);
        Assert.NotEmpty(fixture.ViewModel.Status);
        Assert.Equal(0, fixture.Windows.CloseCount);
        Assert.True(fixture.ViewModel.SaveCommand.CanExecute(null));
        fixture.Service.FailSave = false;
        await fixture.ViewModel.SaveCommand.ExecuteAsync();
        Assert.True(fixture.ViewModel.HasResult);
    }

    [WpfTheory]
    [InlineData("Contrast")]
    [InlineData("Brightness")]
    [InlineData("Saturation")]
    [InlineData("Width")]
    [InlineData("Height")]
    [InlineData("KeepAspectRatio")]
    [InlineData("Reset")]
    public async Task ChangingSettingsDoesNotCancelRenderingOrApplyAnOutdatedPreview(string setting)
    {
        var service = new ControlledPreviewService();
        using var fixture = new Fixture(service);
        var vm = fixture.ViewModel;
        vm.Contrast = 10;
        Task olderPreview = vm.RefreshPreviewAsync();
        await service.FirstRenderStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            switch(setting)
            {
                case "Contrast": vm.Contrast = 70; break;
                case "Brightness": vm.Brightness = 70; break;
                case "Saturation": vm.Saturation = -100; break;
                case "Width": vm.Width = "8"; break;
                case "Height": vm.Height = "3"; break;
                case "KeepAspectRatio": vm.KeepAspectRatio = false; vm.Height = "3"; break;
                case "Reset": vm.ResetCommand.Execute(null); break;
            }
            await vm.RefreshPreviewAsync();
            BitmapSource latest = vm.Preview;
            var expected = await new ImageEditingService().RenderAsync(fixture.Source,
                new(int.Parse(vm.Width), int.Parse(vm.Height), vm.Contrast, vm.Brightness, vm.Saturation));
            Assert.Equal(ImageEditingServiceTests.Pixels(expected), ImageEditingServiceTests.Pixels(latest));
            Assert.False(service.FirstRenderToken.IsCancellationRequested);

            service.ReleaseFirstRender.TrySetResult();
            await olderPreview;
            Assert.Same(latest, vm.Preview);
            Assert.False(vm.IsPreviewBusy);
            Assert.Empty(vm.Status);
        }
        finally
        {
            service.ReleaseFirstRender.TrySetResult();
            await olderPreview;
        }
    }

    [WpfFact]
    public async Task InvalidDimensionsAndClosingDoNotApplyAnOutstandingPreview()
    {
        var service = new ControlledPreviewService();
        using var fixture = new Fixture(service);
        var vm = fixture.ViewModel;
        BitmapSource original = vm.Preview;
        vm.Contrast = 70;
        Task pending = vm.RefreshPreviewAsync();
        await service.FirstRenderStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.Width = string.Empty;
        Assert.False(vm.IsPreviewBusy);
        vm.Dispose();
        service.ReleaseFirstRender.TrySetResult();
        await pending;
        Assert.Same(original, vm.Preview);
        Assert.False(service.FirstRenderToken.IsCancellationRequested);
    }

    [WpfFact]
    public async Task SavingDuringPreviewUsesCurrentSettingsAndDoesNotPublishTheOldPreview()
    {
        using var directory = new ImageEditingServiceTests.TestDirectory();
        var service = new ControlledPreviewService();
        using var fixture = new Fixture(service);
        var vm = fixture.ViewModel;
        BitmapSource original = vm.Preview;
        vm.Contrast = 10;
        Task pending = vm.RefreshPreviewAsync();
        await service.FirstRenderStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            vm.Brightness = 25;
            vm.Saturation = -100;
            vm.Width = "8";
            fixture.Picker.Target = System.IO.Path.Combine(directory.Path, "latest.png");
            await vm.SaveCommand.ExecuteAsync();
            var saved = ImageEditingServiceTests.Read(fixture.Picker.Target);
            var expected = await new ImageEditingService().RenderAsync(fixture.Source,
                new(8, 4, Contrast: 10, Brightness: 25, Saturation: -100));
            Assert.Equal(ImageEditingServiceTests.Pixels(expected), ImageEditingServiceTests.Pixels(saved));
            service.ReleaseFirstRender.TrySetResult();
            await pending;
            Assert.Same(original, vm.Preview);
            Assert.True(vm.HasResult);
            Assert.Equal(1, fixture.Windows.CloseCount);
        }
        finally
        {
            service.ReleaseFirstRender.TrySetResult();
            await pending;
        }
    }

    private sealed class Fixture: IDisposable
    {
        public BitmapSource Source { get; } = ImageEditingServiceTests.Bitmap(4, 2);
        public ImageEditViewModel ViewModel { get; }
        public FakeService Service { get; } = new();
        public FakePicker Picker { get; } = new();
        public WindowProxy Windows { get; }

        public Fixture(IImageEditingService? previewService = null)
        {
            var windows = DispatchProxy.Create<IWindowManager, WindowProxy>();
            Windows = (WindowProxy)windows;
            ViewModel = new ImageEditViewModel(new WindowContext(new Dictionary<string, object?>
            {
                [ImageEditViewModel.RequestContextKey] = new ImageEditRequest(Source, "source.png", false)
            }), previewService ?? Service, Picker, windows);
        }

        public void Dispose() => ViewModel.Dispose();
    }

    internal sealed class FakePicker: IImageSaveTargetPicker
    {
        public string? Target { get; set; }
        public string? Pick(string sourcePath, Guid ownerId) => Target;
    }

    private sealed class FakeService: IImageEditingService
    {
        public bool WaitForCancellation { get; set; }
        public bool FailSave { get; set; }
        public int Saves { get; private set; }
        public ImageEditOptions? SavedOptions { get; private set; }
        public Task<BitmapSource> RenderAsync(BitmapSource source, ImageEditOptions options,
            int? previewMaxDimension = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(source);
        public async Task SaveAsync(BitmapSource source, ImageEditOptions options, string targetPath,
            CancellationToken cancellationToken = default)
        {
            Saves++;
            SavedOptions = options;
            if(FailSave) throw new System.IO.IOException("Disk is full.");
            if(WaitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class ControlledPreviewService: IImageEditingService
    {
        private readonly ImageEditingService service = new();
        private int renderCount;
        public TaskCompletionSource FirstRenderStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstRender { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken FirstRenderToken { get; private set; }

        public async Task<BitmapSource> RenderAsync(BitmapSource source, ImageEditOptions options,
            int? previewMaxDimension = null, CancellationToken cancellationToken = default)
        {
            if(Interlocked.Increment(ref renderCount) == 1)
            {
                FirstRenderToken = cancellationToken;
                FirstRenderStarted.TrySetResult();
                await ReleaseFirstRender.Task;
            }
            return await service.RenderAsync(source, options, previewMaxDimension, cancellationToken);
        }

        public Task SaveAsync(BitmapSource source, ImageEditOptions options, string targetPath,
            CancellationToken cancellationToken = default) => service.SaveAsync(source, options, targetPath, cancellationToken);
    }

    public class WindowProxy: DispatchProxy
    {
        public int CloseCount { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod!.Name == nameof(IWindowManager.CloseWindow)) CloseCount++;
            return targetMethod.ReturnType == typeof(void) ? null :
                targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }
}

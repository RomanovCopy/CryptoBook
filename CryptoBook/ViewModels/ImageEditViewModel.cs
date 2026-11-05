using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CryptoBook.ViewModels;

public sealed class ImageEditViewModel: ViewModelBase, IViewModel, IWindowWithId,
    IDialogResult<string>, IConditionalDialogResult, IDisposable
{
    public const string RequestContextKey = "imageEditRequest";
    private readonly ImageEditRequest request;
    private readonly IImageEditingService service;
    private readonly IImageSaveTargetPicker picker;
    private readonly IWindowManager windows;
    private int previewVersion;
    private bool disposed, isSaving, isPreviewBusy, closeRequested;
    private double contrast, brightness, saturation;
    private string width, height, status = string.Empty;
    private bool keepAspectRatio = true;
    private BitmapSource preview;

    public ImageEditViewModel(IWindowContext context, IImageEditingService service,
        IImageSaveTargetPicker picker, IWindowManager windows)
    {
        request = context.Get<ImageEditRequest>(RequestContextKey);
        this.service = service;
        this.picker = picker;
        this.windows = windows;
        width = request.Source.PixelWidth.ToString(CultureInfo.CurrentCulture);
        height = request.Source.PixelHeight.ToString(CultureInfo.CurrentCulture);
        preview = request.Source;
        SaveCommand = new AsyncRelayCommand((_, token) => SaveAsync(token), _ => CanSave);
        ResetCommand = new RelayCommand(_ => Reset(), _ => CanEdit);
        Loaded = new RelayCommand(_ => _ = RefreshPreviewAsync());
        Close = new RelayCommand(_ => RequestClose());
        Closing = new RelayCommand(parameter =>
        {
            if(IsSaving && parameter is CancelEventArgs args)
            {
                args.Cancel = true;
                closeRequested = true;
                SaveCommand.Cancel();
            }
        });
        Closed = new RelayCommand(_ => Dispose());
    }

    public Guid WindowId { get; } = Guid.NewGuid();
    public string SourceName => Path.GetFileName(request.SourcePath);
    public bool IsEncrypted => request.IsEncrypted;
    public string? Result { get; private set; }
    public bool HasResult => Result is not null;
    public BitmapSource Preview { get => preview; private set => SetProperty(ref preview, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool IsSaving { get => isSaving; private set { SetProperty(ref isSaving, value); NotifyState(); } }
    public bool IsPreviewBusy { get => isPreviewBusy; private set => SetProperty(ref isPreviewBusy, value); }
    public bool CanEdit => !disposed && !IsSaving;
    public bool CanSave => CanEdit && TryGetOptions(out _);
    public string ValidationMessage => TryGetOptions(out _) ? string.Empty : Text("InvalidSize");
    public double Contrast { get => contrast; set { if(SetProperty(ref contrast, value)) Changed(); } }
    public double Brightness { get => brightness; set { if(SetProperty(ref brightness, value)) Changed(); } }
    public double Saturation { get => saturation; set { if(SetProperty(ref saturation, value)) Changed(); } }
    public string Width
    {
        get => width;
        set
        {
            if(!SetProperty(ref width, value)) return;
            if(KeepAspectRatio && ParseSize(value, out int parsed))
                SetProperty(ref height, Proportional(parsed, request.Source.PixelHeight, request.Source.PixelWidth), nameof(Height));
            Changed();
        }
    }
    public string Height
    {
        get => height;
        set
        {
            if(!SetProperty(ref height, value)) return;
            if(KeepAspectRatio && ParseSize(value, out int parsed))
                SetProperty(ref width, Proportional(parsed, request.Source.PixelWidth, request.Source.PixelHeight), nameof(Width));
            Changed();
        }
    }
    public bool KeepAspectRatio
    {
        get => keepAspectRatio;
        set
        {
            if(!SetProperty(ref keepAspectRatio, value)) return;
            if(value && ParseSize(Width, out int parsed))
                SetProperty(ref height, Proportional(parsed, request.Source.PixelHeight, request.Source.PixelWidth), nameof(Height));
            Changed();
        }
    }
    public AsyncRelayCommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand Loaded { get; }
    public ICommand Close { get; }
    public ICommand Closing { get; }
    public ICommand Closed { get; }

    private static string Proportional(int value, int numerator, int denominator) =>
        Math.Max(1, (long)Math.Round((double)value * numerator / denominator))
            .ToString(CultureInfo.CurrentCulture);

    private static bool ParseSize(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.CurrentCulture, out result) && result > 0;

    private bool TryGetOptions(out ImageEditOptions options)
    {
        bool validWidth = ParseSize(Width, out int w);
        bool validHeight = ParseSize(Height, out int h);
        options = new ImageEditOptions(w, h, Contrast, Brightness, Saturation);
        return validWidth && validHeight && ImageEditingService.IsValidSize(w, h);
    }

    private void Changed()
    {
        Status = string.Empty;
        NotifyState();
        _ = RefreshPreviewAsync();
    }

    public async Task RefreshPreviewAsync()
    {
        int version = ++previewVersion;
        if(disposed || IsSaving || !TryGetOptions(out var options))
        {
            IsPreviewBusy = false;
            return;
        }
        IsPreviewBusy = true;
        try
        {
            // Slider changes supersede bounded previews without throwing cancellation
            // exceptions during normal editing, including while debugging.
            await Task.Delay(120);
            if(disposed || version != previewVersion) return;
            BitmapSource result = await service.RenderAsync(request.Source, options, 1000);
            if(!disposed && version == previewVersion) Preview = result;
        }
        catch(Exception exception)
        {
            if(!disposed && version == previewVersion)
                Status = Text("PreviewFailed") + " " + exception.Message;
        }
        finally
        {
            if(version == previewVersion)
                IsPreviewBusy = false;
        }
    }

    private async Task SaveAsync(CancellationToken token)
    {
        if(!TryGetOptions(out var options)) return;
        try
        {
            string? target = picker.Pick(request.SourcePath, WindowId);
            if(target is null) return;
            IsSaving = true;
            Status = Text("Saving");
            InvalidatePreview();
            await service.SaveAsync(request.Source, options, target, token);
            Result = target;
            closeRequested = true;
        }
        catch(OperationCanceledException) { Status = Text("Cancelled"); }
        catch(Exception exception) { Status = Text("SaveFailed") + " " + exception.Message; }
        finally
        {
            IsSaving = false;
            if(closeRequested && !disposed)
                windows.CloseWindow(WindowId);
        }
    }

    private void Reset()
    {
        contrast = brightness = saturation = 0;
        width = request.Source.PixelWidth.ToString(CultureInfo.CurrentCulture);
        height = request.Source.PixelHeight.ToString(CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(Contrast), nameof(Brightness), nameof(Saturation), nameof(Width), nameof(Height));
        Changed();
    }

    private void RequestClose()
    {
        if(IsSaving)
        {
            closeRequested = true;
            SaveCommand.Cancel();
        }
        else windows.CloseWindow(WindowId);
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanEdit), nameof(CanSave), nameof(ValidationMessage));
        SaveCommand.RaiseCanExecuteChanged();
        ((RelayCommand)ResetCommand).RaiseCanExecuteChanged();
    }

    private static string Text(string key) => LocalizationManager.GetString("ImageEdit." + key);

    private void InvalidatePreview()
    {
        previewVersion++;
        IsPreviewBusy = false;
    }

    public void Dispose()
    {
        if(disposed) return;
        disposed = true;
        InvalidatePreview();
        SaveCommand.Cancel();
    }
}

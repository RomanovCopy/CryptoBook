using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;
using CryptoBook.Validation;

using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Xunit;

namespace CryptoBook.Tests;

public sealed class MediaPlayerDeletionTests
{
    [WpfFact]
    public void CancelingWarningKeepsCurrentImageAndDoesNotRecycle()
    {
        using var fixture = new PlayerFixture(".jpg", imageCount: 2);
        fixture.Messages.Confirmed = false;
        string? currentImage = fixture.Image.CurrentImagePath;

        fixture.Model.Execute_DeleteCurrentImage(null);

        Assert.Equal(["plan", "warning", "confirmation"], fixture.Events);
        Assert.Empty(fixture.Bin.Paths);
        Assert.Equal(currentImage, fixture.Image.CurrentImagePath);
        Assert.True(fixture.Model.IsImageVisible);
        Assert.True(fixture.Model.CanExecute_NextImage(null));
        Assert.True(Assert.Single(fixture.Messages.Shown).Cancelable);
    }

    [WpfTheory]
    [InlineData(".jpg")]
    [InlineData(".cbook")]
    [InlineData(".cbox")]
    public void ConfirmedDeletionRecyclesOriginalFileThenOpensNextImage(string extension)
    {
        using var fixture = new PlayerFixture(extension, imageCount: 2);

        fixture.Model.Execute_DeleteCurrentImage(null);

        Assert.Equal(["plan", "warning", "confirmation", "recycle"], fixture.Events);
        Assert.Equal([fixture.Paths[0]], fixture.Bin.Paths);
        var warning = Assert.Single(fixture.Messages.Shown);
        Assert.True(warning.Cancelable);
        Assert.Contains(LocalizationManager.GetString("Explorer.RecoverableDeleteWarning"), warning.Text);
        Assert.Contains(fixture.Paths[0], warning.Text);
        Assert.Equal(PreparedSource.PlaybackPathFor(fixture.Paths[1]), fixture.Image.CurrentImagePath);
        Assert.True(fixture.Model.IsImageVisible);
        Assert.False(fixture.Model.CanExecute_PreviousImage(null));
        Assert.False(fixture.Model.CanExecute_NextImage(null));
    }

    [WpfTheory]
    [InlineData(null)]
    [InlineData("Recycle Bin unavailable")]
    public void RecyclingFailureKeepsImageAndCatalogAndShowsError(string? error)
    {
        using var fixture = new PlayerFixture(".jpg", imageCount: 2);
        fixture.Bin.Result = new FileOperationResult { Success = false, ErrorMessage = error };
        string? currentImage = fixture.Image.CurrentImagePath;

        fixture.Model.Execute_DeleteCurrentImage(null);

        Assert.Equal([fixture.Paths[0]], fixture.Bin.Paths);
        Assert.Equal(currentImage, fixture.Image.CurrentImagePath);
        Assert.True(fixture.Model.IsImageVisible);
        Assert.True(fixture.Model.CanExecute_NextImage(null));
        Assert.True(fixture.Model.CanExecute_DeleteCurrentImage(null));
        Assert.Equal(2, fixture.Messages.Shown.Count);
        Assert.False(fixture.Messages.Shown[1].Cancelable);
        Assert.Equal(error ?? LocalizationManager.GetString("Media.DeleteFailed"),
            fixture.Messages.Shown[1].Text);
    }

    [WpfFact]
    public void RecyclingLastImageClearsViewer()
    {
        using var fixture = new PlayerFixture(".jpg", imageCount: 1);

        fixture.Model.Execute_DeleteCurrentImage(null);

        Assert.Equal([fixture.Paths[0]], fixture.Bin.Paths);
        Assert.Null(fixture.Image.ImageSource);
        Assert.True(fixture.Model.IsEmptyVisible);
        Assert.False(fixture.Model.CanExecute_DeleteCurrentImage(null));
        Assert.True(Assert.Single(fixture.Preparation.Sources).Disposed);
    }

    public static IEnumerable<object[]> PermanentDeletionCases =>
        from extension in new[] { ".jpg", ".cbook", ".cbox" }
        from capacity in new long[] { 0, 100 }
        from consent in new[] { false, true }
        select new object[] { extension, capacity, consent };

    [WpfTheory]
    [MemberData(nameof(PermanentDeletionCases))]
    public async Task FullOrMissingBinRequiresConsentBeforeDeletingOriginal(string extension, long capacity, bool consent)
    {
        using var fixture = new PlayerFixture(extension, imageCount: 2);
        fixture.Bin.Plan = new([new("D:\\", 100, capacity, 90)]);
        fixture.Messages.Confirmed = consent;
        string? originalView = fixture.Image.CurrentImagePath;

        await fixture.Model.DeleteCurrentImageAsync();

        Assert.Equal(consent ? new[] { "plan", "warning", "confirmation", "permanent" }
            : new[] { "plan", "warning", "confirmation" }, fixture.Events);
        Assert.Contains(LocalizationManager.GetString("Explorer.PermanentDeleteWholeSelectionWarning").Split('{')[0],
            Assert.Single(fixture.Messages.Shown).Text);
        Assert.Contains(fixture.Paths[0], fixture.Messages.Shown[0].Text);
        Assert.Equal(!consent, File.Exists(fixture.Paths[0]));
        Assert.True(File.Exists(fixture.Paths[1]));
        Assert.Equal(consent ? new[] { fixture.Paths[0] } : Array.Empty<string>(), fixture.Bin.Paths);
        Assert.Equal(consent ? PreparedSource.PlaybackPathFor(fixture.Paths[1]) : originalView, fixture.Image.CurrentImagePath);
        Assert.Equal(consent, fixture.Preparation.Sources[0].Disposed);
    }

    [WpfFact]
    public async Task UnknownBinCapacityKeepsOriginalAndViewer()
    {
        using var fixture = new PlayerFixture(".cbook", imageCount: 2);
        fixture.Bin.Plan = new([new("D:\\", 100, null, 0)]);
        string? originalView = fixture.Image.CurrentImagePath;
        await fixture.Model.DeleteCurrentImageAsync();
        Assert.Equal(["plan", "error"], fixture.Events);
        Assert.Empty(fixture.Bin.Paths);
        Assert.True(File.Exists(fixture.Paths[0]));
        Assert.Equal(originalView, fixture.Image.CurrentImagePath);
        Assert.False(fixture.Preparation.Sources[0].Disposed);
        Assert.Equal(LocalizationManager.GetString("Explorer.RecycleSpaceUnknown"), Assert.Single(fixture.Messages.Shown).Text);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingOrDisposalDuringPreflightCancelsBeforeAnyMutation(bool dispose)
    {
        using var fixture = new PlayerFixture(".jpg", imageCount: 2);
        fixture.Bin.HoldPlan = true;
        Task deletion = fixture.Model.DeleteCurrentImageAsync();
        await fixture.Bin.PlanStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(fixture.Model.CanExecute_DeleteCurrentImage(null));
        Assert.False(fixture.Model.CanExecute_NextImage(null));
        Assert.False(fixture.Model.CanExecute_OpenFile(null));
        fixture.Model.Execute_DeleteCurrentImage(null);
        fixture.Model.Execute_NextImage(null);
        if(dispose) fixture.Model.Dispose(); else fixture.Model.Execute_Closing(null);
        await deletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["plan"], fixture.Events);
        Assert.Empty(fixture.Bin.Paths);
        Assert.All(fixture.Paths, path => Assert.True(File.Exists(path)));
    }

    [WpfFact]
    public async Task ClosingFromConfirmationDoesNotAcceptConsentOrDelete()
    {
        using var fixture = new PlayerFixture(".jpg", imageCount: 2);
        fixture.Messages.OnWarning = () => fixture.Model.Execute_Closing(null);
        await fixture.Model.DeleteCurrentImageAsync();
        Assert.Equal(["plan", "warning"], fixture.Events);
        Assert.Empty(fixture.Bin.Paths);
        Assert.True(File.Exists(fixture.Paths[0]));
    }

    [WpfFact]
    public async Task ProgressWaitsForNextImageAndDuplicateDeleteIsBlocked()
    {
        using var fixture = new PlayerFixture(".jpg", imageCount: 2);
        fixture.Image.HoldNextLoad = true;
        Task deletion = fixture.Model.DeleteCurrentImageAsync();
        await fixture.Image.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(fixture.Progress.Active);
        Assert.False(deletion.IsCompleted);
        Assert.False(fixture.Model.CanExecute_DeleteCurrentImage(null));
        fixture.Model.Execute_DeleteCurrentImage(null);
        Assert.Single(fixture.Bin.Paths);
        fixture.Image.ReleaseLoad.TrySetResult();
        await deletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(fixture.Progress.Active);
        Assert.Equal(fixture.Paths[1], fixture.Image.CurrentImagePath);
        Assert.True(fixture.Model.CanExecute_DeleteCurrentImage(null));
    }

    [WpfFact]
    public async Task CancellationAfterCompletedSingleDeletionStillAdvancesViewer()
    {
        using var fixture = new PlayerFixture(".jpg", imageCount: 2);
        fixture.Bin.AfterDeletion = fixture.Progress.Cancellation.Cancel;
        await fixture.Model.DeleteCurrentImageAsync();
        Assert.True(fixture.Progress.Result!.Canceled);
        Assert.Equal(1, fixture.Progress.Result.CompletedCount);
        Assert.False(File.Exists(fixture.Paths[0]));
        Assert.Equal(fixture.Paths[1], fixture.Image.CurrentImagePath);
        Assert.Single(fixture.Messages.Shown);
    }

    [WpfFact]
    public async Task RealImageAndNativeRecycleBinDeleteOriginalThenRestoreIt()
    {
        var bin = new NativeBin();
        using var fixture = new PlayerFixture(".png", imageCount: 2, bin, new ImageService());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while(!fixture.Model.CanExecute_DeleteCurrentImage(null)) await Task.Delay(10, timeout.Token);
        byte[] original = File.ReadAllBytes(fixture.Paths[0]);
        fixture.Progress.BeforeClose = () =>
        {
            Assert.Equal(fixture.Paths[1], fixture.Images.CurrentImagePath);
            Assert.NotNull(fixture.Images.ImageSource);
        };
        try
        {
            await fixture.Model.DeleteCurrentImageAsync();
            Assert.True(fixture.Progress.Result!.Success, fixture.Progress.Result.Failure?.ErrorMessage);
            Assert.False(File.Exists(fixture.Paths[0]));
            Assert.Equal(fixture.Paths[1], fixture.Images.CurrentImagePath);
            Assert.NotNull(bin.Result!.Results.Single().AffectedPath);
        }
        finally
        {
            string? recycled = bin.Result?.Results.FirstOrDefault()?.AffectedPath;
            if(recycled is not null && Path.Exists(recycled) && !File.Exists(fixture.Paths[0]))
                await RecycleBinValidation.RestoreAsync(recycled, fixture.Paths[0]);
        }
        Assert.Equal(original, File.ReadAllBytes(fixture.Paths[0]));
    }

    private sealed class NativeBin: IRecycleBinService
    {
        private readonly WindowsRecycleBinService native = new();
        public FileOperationBatchResult? Result { get; private set; }
        public async Task<RecycleBinDeletionPlan> PlanDeletionAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
        {
            RecycleBinDeletionPlan plan = await native.PlanDeletionAsync(paths, cancellationToken);
            Assert.True(plan.Fits, "The disposable native test requires known free Recycle Bin capacity.");
            return plan;
        }
        public Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public async Task<FileOperationBatchResult> RecycleBatchAsync(IReadOnlyList<string> paths, IProgressReporter? progress = null, CancellationToken cancellationToken = default) =>
            Result = await native.RecycleBatchAsync(paths, progress, cancellationToken);
    }

    private sealed class PlayerFixture : IDisposable
    {
        private readonly MediaPlaybackCoordinator coordinator = new();
        public List<string> Events { get; } = [];
        public string[] Paths { get; }
        public ImageProxy Image { get; }
        public MessageStub Messages { get; }
        public RecycleBinStub Bin { get; }
        public MediaPlayerModel Model { get; }
        public ProgressStub Progress { get; } = new();
        public IImageService Images { get; }
        public PreparationStub Preparation { get; } = new();
        private readonly string root;

        public PlayerFixture(string extension, int imageCount, IRecycleBinService? bin = null, IImageService? images = null)
        {
            root = Path.Combine(Path.GetTempPath(), "CryptoBook.MediaDeletion", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Paths = Enumerable.Range(1, imageCount)
                .Select(index => Path.Combine(root, $"image{index}{extension}"))
                .ToArray();
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4);
            foreach(string path in Paths)
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(path); encoder.Save(stream);
            }
            Messages = new(Events);
            Bin = new(Events);
            var imageService = DispatchProxy.Create<IImageService, ImageProxy>();
            Image = (ImageProxy)(object)imageService;
            Images = images ?? imageService;
            var files = new FileOperationCoordinator(
                DispatchProxy.Create<IFileManagerService, PassiveProxy>(), Progress,
                DispatchProxy.Create<IFileConflictResolver, PassiveProxy>(), recycleBin: bin ?? Bin,
                messageService: Messages);
            Model = new MediaPlayerModel(
                DispatchProxy.Create<IMediaPlayerService, PassiveProxy>(),
                Images,
                new WindowContext(new Dictionary<string, object?>()),
                files,
                Messages,
                Preparation,
                new ExplorerStub(new MediaCatalogSelection(Paths[0], Paths)),
                DispatchProxy.Create<IWindowManager, PassiveProxy>(),
                coordinator,
                DispatchProxy.Create<IViewRenderSynchronizationService, PassiveProxy>());
            Model.Execute_OpenFile(null);
            if(images is null)
            {
                Assert.True(Model.IsImageVisible);
                Assert.Equal(PreparedSource.PlaybackPathFor(Paths[0]), Image.CurrentImagePath);
            }
        }

        public void Dispose()
        {
            Model.Dispose();
            coordinator.Dispose();
            Progress.Cancellation.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class MessageStub(List<string> events) : IMessageService
    {
        public bool Confirmed { get; set; } = true;
        public Action? OnWarning { get; set; }
        public List<(string Text, bool Cancelable)> Shown { get; } = [];
        public Task<Guid> ShowMessage(string title, string message, bool isCanceled = false)
        {
            events.Add(isCanceled ? "warning" : "error");
            Shown.Add((message, isCanceled));
            if(isCanceled) OnWarning?.Invoke();
            return Task.FromResult(Guid.NewGuid());
        }
        public bool ShowConfirmation(Guid id)
        {
            events.Add("confirmation");
            return Confirmed;
        }
        public void CloseDialog(Guid id) { }
    }

    private sealed class RecycleBinStub(List<string> events) : IRecycleBinService
    {
        public List<string> Paths { get; } = [];
        public FileOperationResult Result { get; set; } = FileOperationResult.Ok();
        public RecycleBinDeletionPlan Plan { get; set; } = new([new("D:\\", 100, 1000, 0)]);
        public bool HoldPlan { get; set; }
        public TaskCompletionSource PlanStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleasePlan { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action? AfterDeletion { get; set; }
        public async Task<RecycleBinDeletionPlan> PlanDeletionAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
        {
            events.Add("plan");
            PlanStarted.TrySetResult();
            if(HoldPlan) await ReleasePlan.Task.WaitAsync(cancellationToken);
            return Plan;
        }
        public Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default)
        {
            events.Add("recycle");
            Paths.Add(path);
            if(!Result.Success) return Task.FromResult(Result);
            string bin = Path.Combine(Path.GetDirectoryName(path)!, ".test-bin");
            Directory.CreateDirectory(bin);
            string recycled = Path.Combine(bin, Path.GetFileName(path));
            File.Move(path, recycled);
            AfterDeletion?.Invoke();
            return Task.FromResult(FileOperationResult.Ok(recycled));
        }
        public Task<FileOperationBatchResult> DeletePermanentlyAsync(IReadOnlyList<string> paths,
            IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            events.Add("permanent"); Paths.AddRange(paths);
            if(!Result.Success) return Task.FromResult(new FileOperationBatchResult([Result], 0, 0, false, false));
            foreach(string path in paths) File.Delete(path);
            AfterDeletion?.Invoke();
            return Task.FromResult(new FileOperationBatchResult(paths.Select(_ => FileOperationResult.Ok()).ToArray(), paths.Count, 0,
                cancellationToken.IsCancellationRequested, true) { PermanentlyDeletedCount = paths.Count });
        }
    }

    private sealed class ExplorerStub(MediaCatalogSelection selection) : IFileExplorerService
    {
        public void Show(string? initialDirectory = null) => throw new NotSupportedException();
        public void ShowFileSelection(string? initialDirectory, Action<MediaCatalogSelection> fileSelected) =>
            fileSelected(selection);
    }

    private sealed class PreparationStub : IMediaSourcePreparationService
    {
        public List<PreparedSource> Sources { get; } = [];
        public Task<IPreparedMediaSource> PrepareAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            var source = new PreparedSource(sourcePath); Sources.Add(source);
            return Task.FromResult<IPreparedMediaSource>(source);
        }
    }

    private sealed class PreparedSource(string path) : IPreparedMediaSource
    {
        public string OriginalPath => path;
        public string OriginalExtension => ".jpg";
        public bool IsEncrypted => Path.GetExtension(path) is ".cbook" or ".cbox";
        public bool IsTemporary => IsEncrypted;
        public string PlaybackPath => PlaybackPathFor(path);
        public Stream? PlaybackStream => null;
        public static string PlaybackPathFor(string path) => Path.GetExtension(path) is ".cbook" or ".cbox"
            ? Path.ChangeExtension(path, ".decrypted.jpg") : path;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    public class ImageProxy : PassiveProxy
    {
        public ImageSource? ImageSource { get; private set; }
        public string? CurrentImagePath { get; private set; }
        public bool HoldNextLoad { get; set; }
        public TaskCompletionSource LoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseLoad { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch(targetMethod?.Name)
            {
                case "LoadImageAsync":
                    if(HoldNextLoad)
                    {
                        HoldNextLoad = false;
                        LoadStarted.TrySetResult();
                        return FinishLoadAsync((string)args![0]!);
                    }
                    CurrentImagePath = (string)args![0]!;
                    ImageSource = new DrawingImage();
                    return Task.CompletedTask;
                case "get_ImageSource": return ImageSource;
                case "get_CurrentImagePath": return CurrentImagePath;
                case "Clear":
                    CurrentImagePath = null;
                    ImageSource = null;
                    return null;
                default: return base.Invoke(targetMethod, args);
            }
        }
        private async Task FinishLoadAsync(string path)
        {
            await ReleaseLoad.Task;
            CurrentImagePath = path; ImageSource = new DrawingImage();
        }
    }

    private sealed class ProgressStub: IProgressDialogService, IProgressReporter
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public bool Active { get; private set; }
        public FileOperationBatchResult? Result { get; private set; }
        public Action? BeforeClose { get; set; }
        public async Task<T> RunAsync<T>(string operationName, Func<IProgressReporter, CancellationToken, Task<T>> operation)
        {
            Active = true;
            try { T result = await operation(this, Cancellation.Token); Result = result as FileOperationBatchResult; return result; }
            finally { try { BeforeClose?.Invoke(); } finally { Active = false; } }
        }
        public void Report(double? value, string? currentInfo = null) { }
    }

    public class PassiveProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void)
                ? Activator.CreateInstance(type) : null;
    }
}

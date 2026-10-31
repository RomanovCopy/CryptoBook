using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;

using System.IO;
using System.Reflection;
using System.Windows.Media;

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

        Assert.Equal(["warning", "confirmation"], fixture.Events);
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

        Assert.Equal(["warning", "confirmation", "recycle"], fixture.Events);
        Assert.Equal([fixture.Paths[0]], fixture.Bin.Paths);
        var warning = Assert.Single(fixture.Messages.Shown);
        Assert.True(warning.Cancelable);
        Assert.Equal(LocalizationManager.Format("Media.DeletePrompt",
            Path.GetFileName(fixture.Paths[0])), warning.Text);
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

        public PlayerFixture(string extension, int imageCount)
        {
            string root = Path.Combine(Path.GetTempPath(), "CryptoBook.MediaDeletion", Guid.NewGuid().ToString("N"));
            Paths = Enumerable.Range(1, imageCount)
                .Select(index => Path.Combine(root, $"image{index}{extension}"))
                .ToArray();
            Messages = new(Events);
            Bin = new(Events);
            var imageService = DispatchProxy.Create<IImageService, ImageProxy>();
            Image = (ImageProxy)(object)imageService;
            Model = new MediaPlayerModel(
                DispatchProxy.Create<IMediaPlayerService, PassiveProxy>(),
                imageService,
                new WindowContext(new Dictionary<string, object?>()),
                Bin,
                Messages,
                new PreparationStub(),
                new ExplorerStub(new MediaCatalogSelection(Paths[0], Paths)),
                DispatchProxy.Create<IWindowManager, PassiveProxy>(),
                coordinator);
            Model.Execute_OpenFile(null);
            Assert.True(Model.IsImageVisible);
            Assert.Equal(PreparedSource.PlaybackPathFor(Paths[0]), Image.CurrentImagePath);
        }

        public void Dispose()
        {
            Model.Dispose();
            coordinator.Dispose();
        }
    }

    private sealed class MessageStub(List<string> events) : IMessageService
    {
        public bool Confirmed { get; set; } = true;
        public List<(string Text, bool Cancelable)> Shown { get; } = [];
        public Task<Guid> ShowMessage(string title, string message, bool isCanceled = false)
        {
            events.Add(isCanceled ? "warning" : "error");
            Shown.Add((message, isCanceled));
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
        public Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default)
        {
            events.Add("recycle");
            Paths.Add(path);
            return Task.FromResult(Result);
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
        public Task<IPreparedMediaSource> PrepareAsync(string sourcePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<IPreparedMediaSource>(new PreparedSource(sourcePath));
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
        public void Dispose() { }
    }

    public class ImageProxy : PassiveProxy
    {
        public ImageSource? ImageSource { get; private set; }
        public string? CurrentImagePath { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch(targetMethod?.Name)
            {
                case "LoadImageAsync":
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
    }

    public class PassiveProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void)
                ? Activator.CreateInstance(type) : null;
    }
}

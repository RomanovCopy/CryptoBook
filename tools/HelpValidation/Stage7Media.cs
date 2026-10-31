using Autofac;
using CryptoBook.Interfaces;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using MediaPlayer = CryptoBook.Views.MediaPlayer;

internal static partial class Program
{
    private static async Task ValidateStage7Media(IsolatedServices isolated)
    {
        var c = isolated.Container;
        c.Resolve<IThemeManager>().ApplyTheme(CryptoBook.DTO.ApplicationTheme.Light);
        string path = Path.Combine(isolated.DemoDirectory, "Stage7-audio-video.avi");
        MakeAcceptanceAvi(path);
        var windows = c.Resolve<IWindowManager>();
        var coordinator = c.Resolve<IMediaPlaybackCoordinator>();
        var firstId = windows.CreateWindow<MediaPlayer>(new Dictionary<string, object?> { ["path"] = path });
        windows.ShowWindow(firstId);
        var firstWindow = (MediaPlayer)windows.FindHostWindow(firstId)!.Window;
        var first = (IMediaPlayerViewModel)firstWindow.DataContext;
        await Until(() => first.VideoService.IsMediaLoaded);
        first.VideoService.Pause(); first.VideoService.Volume = 20;
        var native = (FlyleafLib.MediaPlayer.Player)first.VideoService.PlayerInstance;
        Check(first.VideoService.Duration.TotalSeconds >= 29 && native.Audio.StreamIndex >= 0, "Generated 30-second AVI loads a real video and a PCM audio stream.");
        first.VideoService.Seek(TimeSpan.FromSeconds(1)); await Task.Delay(350);
        var frame = native.TakeSnapshotToBitmapSource();
        Check(frame is not null && frame.PixelWidth == 320 && frame.PixelHeight == 180, "Flyleaf returns an actual 320x180 decoded video frame.");
        static byte[] Pixels(BitmapSource bitmap)
        {
            int stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(pixels, stride, 0); return pixels;
        }
        byte[] firstPixels = Pixels(frame!);
        Check(firstPixels.Distinct().Count() > 3, "Decoded frame contains the generated colored bars, not a solid placeholder.");
        WriteBitmap(frame!, Path.Combine(isolated.Root, "flyleaf-frame-1.png"));
        first.VideoService.Seek(TimeSpan.FromSeconds(2)); await Task.Delay(350);
        var secondFrame = native.TakeSnapshotToBitmapSource();
        Check(secondFrame is not null && !SHA256.HashData(firstPixels).SequenceEqual(SHA256.HashData(Pixels(secondFrame))), "Seeking changes the actual decoded frame pixels.");
        WriteBitmap(secondFrame!, Path.Combine(isolated.Root, "flyleaf-frame-2.png"));
        var peerId = windows.CreateWindow<MediaPlayer>(new Dictionary<string, object?> { ["path"] = path });
        windows.ShowWindow(peerId);
        var peer = (IMediaPlayerViewModel)windows.FindHostWindow(peerId)!.Window.DataContext;
        await Until(() => peer.VideoService.IsMediaLoaded); peer.VideoService.Pause();
        firstWindow.Activate(); coordinator.Activate(firstId);
        first.ToggleSynchronizationCommand.Execute(null);
        Check(first.IsSynchronizationEnabled && peer.IsSynchronizationEnabled, "Experimental synchronization is enabled in both actual players.");
        first.VideoService.Play(); await Until(() => peer.VideoService.IsPlaying);
        Check(first.VideoService.IsPlaying && peer.VideoService.IsPlaying, "Starting the active actual player starts its synchronized peer.");
        first.VideoService.Pause(); await Until(() => !peer.VideoService.IsPlaying);
        Check(!peer.VideoService.IsPlaying, "Pausing the active actual player pauses its synchronized peer.");
        first.VideoService.Seek(TimeSpan.FromSeconds(12)); await Task.Delay(600);
        double drift = Math.Abs(first.VideoService.Position.TotalSeconds - peer.VideoService.Position.TotalSeconds);
        Check(drift < .7 && peer.VideoService.Position.TotalSeconds > 10, "Seeking the active actual player moves its synchronized peer near the same position.");
        first.ToggleSynchronizationCommand.Execute(null);
        first.VideoService.Seek(TimeSpan.FromSeconds(3)); await Task.Delay(350);
        Check(peer.VideoService.Position.TotalSeconds > 10 && first.VideoService.Position.TotalSeconds < 5, "Disabling synchronization restores independent seek control.");
        first.VideoService.Play(); peer.VideoService.Play(); await Task.Delay(250);
        first.PauseAllCommand.Execute(null); await Task.Delay(200);
        Check(!first.VideoService.IsPlaying && !peer.VideoService.IsPlaying, "Pause all pauses both actual players after independent playback.");
        WriteJson(Path.Combine(isolated.Root, "media-acceptance.json"), new
        {
            culture, commit, frameSource = "Flyleaf TakeSnapshotToBitmapSource, actual decoder output, no compositing",
            duration = first.VideoService.Duration.TotalSeconds, audioStreamIndex = native.Audio.StreamIndex, synchronizationDriftSeconds = drift,
            frameFiles = new[] { "flyleaf-frame-1.png", "flyleaf-frame-2.png" },
            limitations = "Audio stream loading does not prove audible sound. Physical native controls and audibility require human observations. Synchronization is experimental and not guaranteed frame-accurate."
        });
        windows.CloseWindow(peerId); windows.CloseWindow(firstId);
    }
}

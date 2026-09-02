using System.Windows;
using System.IO;

using CryptoBook.Services;

using FlyleafLib;
using FlyleafLib.Controls.WPF;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length != 1)
        {
            Console.Error.WriteLine(
                "Usage: dotnet run --project tools/FlyleafPlaybackSmoke -- " +
                "<media-file|--template-only>");
            return 2;
        }

        string? mediaPath = null;
        if(!string.Equals(
            args[0],
            "--template-only",
            StringComparison.OrdinalIgnoreCase))
        {
            mediaPath = Path.GetFullPath(args[0]);
            if(!File.Exists(mediaPath))
            {
                Console.Error.WriteLine($"Media file does not exist: {mediaPath}");
                return 2;
            }
        }

        int exitCode = 1;
        var application = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        application.Startup += async (_, _) =>
        {
            exitCode = await RunSmokeAsync(mediaPath);
            application.Shutdown(exitCode);
        };

        application.Run();
        return exitCode;
    }

    private static async Task<int> RunSmokeAsync(string? mediaPath)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            ValidateFlyleafControlTemplate();
            if(mediaPath is null)
            {
                Console.WriteLine("FLYLEAF_TEMPLATE_SMOKE: PASS");
                return 0;
            }

            using var mediaPlayer = new MediaPlayerService();
            Engine.Config.LogOutput = ":console";
            Engine.Config.LogLevel = LogLevel.Debug;
            Engine.Config.FFmpegLogLevel = Flyleaf.FFmpeg.LogLevel.Warn;

            await mediaPlayer.OpenAsync(mediaPath, autoPlay: true, timeout.Token);

            if(!mediaPlayer.IsMediaLoaded)
            {
                throw new InvalidOperationException(
                    "Flyleaf completed Open but cannot play the media.");
            }

            if(mediaPlayer.Duration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "Flyleaf opened the media without a positive duration.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), timeout.Token);
            if(mediaPlayer.Position <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "Flyleaf opened the media but playback did not advance.");
            }

            mediaPlayer.Pause();

            Console.WriteLine("FLYLEAF_PLAYBACK_SMOKE: PASS");
            Console.WriteLine($"Source: {mediaPath}");
            Console.WriteLine($"Duration: {mediaPlayer.Duration:c}");
            Console.WriteLine($"Position: {mediaPlayer.Position:c}");
            return 0;
        }
        catch(Exception exception)
        {
            Console.Error.WriteLine("FLYLEAF_PLAYBACK_SMOKE: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ValidateFlyleafControlTemplate()
    {
        var host = new FlyleafME();
        host.ApplyTemplate();
        host.Measure(new Size(960, 600));
        host.Arrange(new Rect(0, 0, 960, 600));
        host.UpdateLayout();
    }
}

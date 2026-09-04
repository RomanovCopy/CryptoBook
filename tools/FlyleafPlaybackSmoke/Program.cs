using System.Windows;
using System.IO;

using CryptoBook.Services;
using CryptoBook.Security;

using FlyleafLib;
using FlyleafLib.Controls.WPF;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length is < 1 or > 2)
        {
            Console.Error.WriteLine(
                "Usage: dotnet run --project tools/FlyleafPlaybackSmoke -- " +
                "<media-file|--template-only> [--secure-stream]");
            return 2;
        }

        bool secureStream = args.Length == 2 && string.Equals(
            args[1],
            "--secure-stream",
            StringComparison.OrdinalIgnoreCase);
        if(args.Length == 2 && !secureStream)
            return 2;

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
            exitCode = secureStream
                ? await RunSecureStreamSmokeAsync(mediaPath!)
                : await RunSmokeAsync(mediaPath);
            application.Shutdown(exitCode);
        };

        application.Run();
        return exitCode;
    }

    private static async Task<int> RunSecureStreamSmokeAsync(string mediaPath)
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"CryptoBook.FlyleafSecureStream.{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        string firstEncrypted = Path.Combine(testDirectory, "first.cbook");
        string secondEncrypted = Path.Combine(testDirectory, "second.cbook");

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var codec = new SecureFileV2Codec(
                new FixedKeyProvider(),
                new SecureFileV2Options { ChunkSize = 4096 });
            await codec.EncryptFileAsync(mediaPath, firstEncrypted, cancellationToken: timeout.Token);
            await codec.EncryptFileAsync(mediaPath, secondEncrypted, cancellationToken: timeout.Token);

            await using DecryptedFileContent first =
                await codec.OpenDecryptedReadStreamAsync(firstEncrypted, timeout.Token);
            await using DecryptedFileContent second =
                await codec.OpenDecryptedReadStreamAsync(secondEncrypted, timeout.Token);
            using var mediaPlayer = new MediaPlayerService();
            Engine.Config.LogOutput = ":console";
            Engine.Config.LogLevel = LogLevel.Debug;
            Engine.Config.FFmpegLogLevel = Flyleaf.FFmpeg.LogLevel.Warn;

            await mediaPlayer.OpenAsync(
                first.Content,
                firstEncrypted,
                autoPlay: true,
                timeout.Token);
            var flyleafPlayer =
                (FlyleafLib.MediaPlayer.Player)mediaPlayer.PlayerInstance;
            if(!string.Equals(
                   flyleafPlayer.Playlist.Selected?.Title,
                   Path.GetFileName(firstEncrypted),
                   StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Encrypted media filename is missing from the seek bar.");
            }
            await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);

            mediaPlayer.FrameForward();
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
            if(mediaPlayer.Position < TimeSpan.FromSeconds(4.5))
                throw new InvalidOperationException(
                    $"Secure stream forward button seek failed: {mediaPlayer.Position:c}.");

            mediaPlayer.FrameBackward();
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
            if(mediaPlayer.Position > TimeSpan.FromSeconds(1))
                throw new InvalidOperationException(
                    $"Secure stream backward button seek failed: {mediaPlayer.Position:c}.");

            mediaPlayer.Seek(TimeSpan.FromSeconds(4));
            await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
            if(mediaPlayer.Position < TimeSpan.FromSeconds(3))
                throw new InvalidOperationException(
                    $"Secure stream seek failed: {mediaPlayer.Position:c}.");

            await mediaPlayer.OpenAsync(
                second.Content,
                secondEncrypted,
                autoPlay: true,
                timeout.Token);
            await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
            if(!mediaPlayer.IsMediaLoaded || mediaPlayer.Position <= TimeSpan.Zero)
                throw new InvalidOperationException(
                    "Flyleaf did not advance after switching secure streams.");
            if(!string.Equals(
                   flyleafPlayer.Playlist.Selected?.Title,
                   Path.GetFileName(secondEncrypted),
                   StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Encrypted media filename was not updated after switching streams.");
            }

            Console.WriteLine("FLYLEAF_SECURE_STREAM_SMOKE: PASS");
            return 0;
        }
        catch(Exception exception)
        {
            Console.Error.WriteLine("FLYLEAF_SECURE_STREAM_SMOKE: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if(Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
        }
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

    private sealed class FixedKeyProvider: IKeyProvider
    {
        private static readonly byte[] Key = Enumerable.Range(1, 32)
            .Select(value => (byte)value)
            .ToArray();

        public bool HasKey => true;
        public void SetKey(ReadOnlySpan<char> password) { }
        public byte[] DeriveKey(byte[] salt) => Key.ToArray();
        public Task<byte[]> DeriveKeyAsync(
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Key.ToArray());
        }
        public void Clear() { }
    }
}

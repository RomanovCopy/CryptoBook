using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using FlyleafLib;

using FlyleafPlayer = FlyleafLib.MediaPlayer.Player;

namespace CryptoBook.Services
{
    public class MediaPlayerService: INotifyPropertyChanged, IMediaPlayerService
    {
        private static readonly TimeSpan EndSeekGuard =
            TimeSpan.FromMilliseconds(250);

        private readonly FlyleafPlayer player;
        private TaskCompletionSource<bool>? openCompletion;
        private CancellationTokenRegistration openCancellation;
        private bool disposed;
        private string? source;

        private List<string> _audioStreams = [];
        private List<string> _subtitleStreams = [];

        public object PlayerInstance => player;

        public MediaPlayerService() : this(CreatePlayer())
        {
        }

        public MediaPlayerService(FlyleafPlayer player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.player.OpenCompleted += OnOpenCompleted;
            this.player.PlaybackStopped += OnPlaybackStopped;
            this.player.PropertyChanged += OnPlayerPropertyChanged;
            this.player.Audio.PropertyChanged += OnAudioPropertyChanged;
        }

        public event EventHandler? MediaOpened;
        public event EventHandler<string>? MediaFailed;
        public event EventHandler? MediaEnded;
        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Source => source;

        public TimeSpan Position
        {
            get => TimeSpan.FromTicks(player.CurTime);
            set { player.Seek((int)value.TotalMilliseconds); OnPropertyChanged(); }
        }

        //public TimeSpan Position => TimeSpan.FromTicks(player.CurTime);
        public TimeSpan Duration => TimeSpan.FromTicks(player.Duration);
        public bool IsPlaying => player.IsPlaying;
        public bool IsMediaLoaded => player.CanPlay;

        public double Volume
        {
            get => player.Audio.Volume;
            set => player.Audio.Volume = (int)Math.Clamp(Math.Round(value), 0, 100);
        }

        public bool IsMuted
        {
            get => player.Audio.Mute;
            set => player.Audio.Mute = value;
        }

        // Управление скоростью (Flyleaf принимает значения, где 1000 = 1.0x, 2000 = 2.0x и т.д.)
        public double PlaybackSpeed
        {
            get => player.Speed / 1000.0;
            set { player.Speed = (int)(value * 1000); OnPropertyChanged(); }
        }

        public IReadOnlyList<string> AudioStreams => _audioStreams;
        public int CurrentAudioStreamIndex => player.Audio.StreamIndex;
        public IReadOnlyList<string> SubtitleStreams => _subtitleStreams;
        public int CurrentSubtitleStreamIndex=> player.Subtitles.StreamIndex;

        public Task OpenAsync(
            string source,
            bool autoPlay = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source);
            return OpenCoreAsync(
                source,
                autoPlay,
                cancellationToken,
                () => player.OpenAsync(source));
        }

        public Task OpenAsync(
            Stream source,
            string sourceName,
            bool autoPlay = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
            if(!source.CanRead || !source.CanSeek)
                throw new ArgumentException(
                    "Медиапоток должен поддерживать чтение и позиционирование.",
                    nameof(source));

            return OpenCoreAsync(
                sourceName,
                autoPlay,
                cancellationToken,
                () => player.OpenAsync(source));
        }

        private Task OpenCoreAsync(
            string sourceName,
            bool autoPlay,
            CancellationToken cancellationToken,
            Action open)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            CancelPendingOpen();

            source = sourceName;
            player.Config.Player.AutoPlay = autoPlay;
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            openCompletion = completion;

            if(cancellationToken.CanBeCanceled)
            {
                openCancellation = cancellationToken.Register(() =>
                {
                    player.Stop();
                    completion.TrySetCanceled(cancellationToken);
                });
            }

            if(completion.Task.IsCanceled)
                return completion.Task;

            OnPropertyChanged(nameof(Source), nameof(IsMediaLoaded));
            try
            {
                open();
            }
            catch
            {
                CancelPendingOpen();
                throw;
            }

            return completion.Task;
        }

        public void Play()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            player.Play();
        }

        public void Pause()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            player.Pause();
        }

        public void Stop()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            player.Stop();
        }

        // Переход на шаг встроенной настройки Flyleaf. Не вызываем
        // SeekForward/SeekBackward на границах: FFmpeg может отклонить
        // переход ровно в Duration и отбросить позицию к началу.
        public void FrameForward() => SeekRelative(player.Config.Player.SeekOffset);
        public void FrameBackward() => SeekRelative(-player.Config.Player.SeekOffset);

        public void Seek(TimeSpan position)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            var boundedPosition = ClampSeekPosition(position, Duration);

            var milliseconds = Math.Clamp(
                boundedPosition.TotalMilliseconds,
                0,
                int.MaxValue);

            player.SeekAccurate((int)milliseconds);
        }

        internal static TimeSpan ClampSeekPosition(
            TimeSpan requested,
            TimeSpan duration)
        {
            if(requested <= TimeSpan.Zero || duration <= TimeSpan.Zero)
                return TimeSpan.Zero;

            TimeSpan lastSeekablePosition = duration > EndSeekGuard
                ? duration - EndSeekGuard
                : TimeSpan.Zero;
            return requested >= lastSeekablePosition
                ? lastSeekablePosition
                : requested;
        }

        private void SeekRelative(long offsetTicks)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if(!player.CanPlay || offsetTicks == 0)
                return;

            TimeSpan current = TimeSpan.FromTicks(player.CurTime);
            TimeSpan target;
            try
            {
                target = current + TimeSpan.FromTicks(offsetTicks);
            }
            catch(OverflowException)
            {
                target = offsetTicks > 0 ? TimeSpan.MaxValue : TimeSpan.Zero;
            }

            TimeSpan bounded = ClampSeekPosition(
                target,
                TimeSpan.FromTicks(player.Duration));
            if(bounded == current)
                return;

            player.SeekAccurate((int)Math.Clamp(
                bounded.TotalMilliseconds,
                0,
                int.MaxValue));
        }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            CancelPendingOpen();
            player.OpenCompleted -= OnOpenCompleted;
            player.PlaybackStopped -= OnPlaybackStopped;
            player.PropertyChanged -= OnPlayerPropertyChanged;
            player.Audio.PropertyChanged -= OnAudioPropertyChanged;
            player.Dispose();
            GC.SuppressFinalize(this);
        }

        private static FlyleafPlayer CreatePlayer()
        {
            if(!Engine.IsLoaded)
            {
                var engineConfig = new EngineConfig
                {
                    FFmpegPath = ResolveFFmpegPath()
                };
                Engine.Start(engineConfig);
            }

            return new FlyleafPlayer(new Config());
        }

        internal static IReadOnlyList<string> RequiredFfmpegLibraries { get; } =
        [
            "avcodec-63.dll",
            "avdevice-63.dll",
            "avfilter-12.dll",
            "avformat-63.dll",
            "avutil-61.dll",
            "swresample-7.dll",
            "swscale-10.dll"
        ];

        internal static string ResolveFFmpegPath()
        {
            // В single-file native assets извлекаются в каталог, переданный host
            // через NATIVE_DLL_SEARCH_DIRECTORIES. При обычной сборке они лежат
            // рядом с приложением или в стандартном NuGet RID layout.
            string[] candidates = GetNativeSearchDirectories()
                .SelectMany(directory => new[]
                {
                    directory,
                    Path.Combine(directory, "runtimes", "win-x64", "native")
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach(var candidate in candidates)
            {
                if(RequiredFfmpegLibraries.All(
                    library => File.Exists(Path.Combine(candidate, library))))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException(
                LocalizationManager.Format(
                    "Media.FfmpegMissing",
                    string.Join(" | ", candidates)));
        }

        private static IEnumerable<string> GetNativeSearchDirectories()
        {
            yield return AppContext.BaseDirectory;

            if(AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is not string searchDirectories)
                yield break;

            foreach(string directory in searchDirectories.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return directory;
            }
        }

        private void OnOpenCompleted( object? sender, FlyleafLib.MediaPlayer.OpenCompletedArgs e)
        {
            openCancellation.Dispose();

            if(e.Success)
            {
                ApplySourceTitle();

                // Flyleaf leaves a non-autoplay video paused without rendering a frame.
                // Render the first frame explicitly so the player has a poster before Play.
                if(!player.Config.Player.AutoPlay && player.Video.IsOpened)
                    player.ShowFrame(0);

                openCompletion?.TrySetResult(true);
                MediaOpened?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                var error = string.IsNullOrWhiteSpace(e.Error)
                    ? LocalizationManager.GetString("Media.OpenFailed")
                    : e.Error;

                openCompletion?.TrySetException(new InvalidOperationException(error));
                MediaFailed?.Invoke(this, error);
            }

            openCompletion = null;
            OnPropertyChanged(
                nameof(Position),
                nameof(Duration),
                nameof(IsPlaying),
                nameof(IsMediaLoaded));
        }

        private void OnPlaybackStopped( object? sender, FlyleafLib.MediaPlayer.PlaybackStoppedArgs e)
        {
            if(e.Success && player.Status == FlyleafLib.MediaPlayer.Status.Ended)
            {
                MediaEnded?.Invoke(this, EventArgs.Empty);
            }
            else if(!e.Success && !string.IsNullOrWhiteSpace(e.Error))
            {
                MediaFailed?.Invoke(this, e.Error);
            }

            OnPropertyChanged(nameof(Position), nameof(IsPlaying), nameof(IsMediaLoaded));
        }

        private void OnPlayerPropertyChanged( object? sender, PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(FlyleafPlayer.CurTime):
                    OnPropertyChanged(nameof(Position));
                    break;
                case nameof(FlyleafPlayer.Duration):
                    OnPropertyChanged(nameof(Duration));
                    break;
                case nameof(FlyleafPlayer.IsPlaying):
                    OnPropertyChanged(nameof(IsPlaying));
                    break;
                case nameof(FlyleafPlayer.Speed):
                    OnPropertyChanged(nameof(PlaybackSpeed));
                    break;
                case nameof(FlyleafPlayer.Status):
                case nameof(FlyleafPlayer.CanPlay):
                    OnPropertyChanged(nameof(IsMediaLoaded));
                    break;
            }
        }

        private void OnAudioPropertyChanged( object? sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(FlyleafLib.MediaPlayer.Audio.Volume))
                OnPropertyChanged(nameof(Volume));
            else if(e.PropertyName == nameof(FlyleafLib.MediaPlayer.Audio.Mute))
                OnPropertyChanged(nameof(IsMuted));
        }

        private void CancelPendingOpen()
        {
            openCancellation.Dispose();
            openCompletion?.TrySetCanceled();
            openCompletion = null;
        }

        private void ApplySourceTitle()
        {
            if(player.Playlist.Selected is null ||
               string.IsNullOrWhiteSpace(source))
            {
                return;
            }

            string title = Path.GetFileName(source);
            player.Playlist.Selected.Title = string.IsNullOrWhiteSpace(title)
                ? source
                : title;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void OnPropertyChanged(params string[] names)
        {
            foreach(var name in names)
                OnPropertyChanged(name);
        }

    }
}

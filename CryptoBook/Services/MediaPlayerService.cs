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

        public Task OpenAsync( string source, bool autoPlay = true, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(source);

            CancelPendingOpen();

            this.source = source;
            player.Config.Player.AutoPlay = autoPlay;
            openCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if(cancellationToken.CanBeCanceled)
            {
                openCancellation = cancellationToken.Register(() =>
                {
                    player.Stop();
                    openCompletion?.TrySetCanceled(cancellationToken);
                });
            }

            OnPropertyChanged(nameof(Source), nameof(IsMediaLoaded));
            player.OpenAsync(source);

            return openCompletion.Task;
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

        // Покадровая прокрутка встроенными методами Flyleaf
        public void FrameForward() => player.SeekForward();
        public void FrameBackward() => player.SeekBackward();

        public void Seek(TimeSpan position)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            var boundedPosition = position < TimeSpan.Zero
                ? TimeSpan.Zero
                : position > Duration
                    ? Duration
                    : position;

            var milliseconds = Math.Clamp(
                boundedPosition.TotalMilliseconds,
                0,
                int.MaxValue);

            player.SeekAccurate((int)milliseconds);
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

        private static string ResolveFFmpegPath()
        {
            // При RID-сборке/publish native assets копируются рядом с exe.
            // В обычном NuGet layout они могут оставаться в runtimes/<rid>/native.
            var candidates = new[]
            {
                AppContext.BaseDirectory,
                Path.Combine(
                    AppContext.BaseDirectory,
                    "runtimes",
                    "win-x64",
                    "native")
            };

            foreach(var candidate in candidates)
            {
                if(File.Exists(Path.Combine(candidate, "avcodec-61.dll")) &&
                   File.Exists(Path.Combine(candidate, "avutil-59.dll")))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException(
                "Не найдены нативные библиотеки FFmpeg. Ожидался полный " +
                $"FFmpeg 7.1 runtime в '{string.Join("' или '", candidates)}'.");
        }

        private void OnOpenCompleted( object? sender, FlyleafLib.MediaPlayer.OpenCompletedArgs e)
        {
            openCancellation.Dispose();

            if(e.Success)
            {
                openCompletion?.TrySetResult(true);
                MediaOpened?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                var error = string.IsNullOrWhiteSpace(e.Error)
                    ? "Не удалось открыть медиафайл."
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

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void OnPropertyChanged(params string[] names)
        {
            foreach(var name in names)
                OnPropertyChanged(name);
        }

    }
}

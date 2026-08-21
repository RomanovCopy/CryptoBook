using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.ComponentModel;
using System.Runtime.CompilerServices;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class MediaPlaybackCoordinatorTests
    {
        [Fact]
        public void Activate_AllowsSoundOnlyForActiveWindow()
        {
            using var coordinator = new MediaPlaybackCoordinator();
            var first = new TestPlayer();
            var second = new TestPlayer();
            Guid firstId = Guid.NewGuid();
            Guid secondId = Guid.NewGuid();

            coordinator.Register(firstId, first);
            coordinator.Register(secondId, second);
            Assert.True(first.IsMuted);
            Assert.True(second.IsMuted);

            coordinator.Activate(firstId);
            Assert.False(first.IsMuted);
            Assert.True(second.IsMuted);

            coordinator.Activate(secondId);
            Assert.True(first.IsMuted);
            Assert.False(second.IsMuted);

            coordinator.Deactivate(secondId);
            Assert.True(first.IsMuted);
            Assert.True(second.IsMuted);
        }

        [Fact]
        public void Activation_RestoresUsersMuteChoice()
        {
            using var coordinator = new MediaPlaybackCoordinator();
            var player = new TestPlayer();
            Guid windowId = Guid.NewGuid();
            coordinator.Register(windowId, player);

            coordinator.Activate(windowId);
            player.IsMuted = true;
            coordinator.Deactivate(windowId);
            coordinator.Activate(windowId);

            Assert.True(player.IsMuted);
        }

        [Fact]
        public void PauseAll_PausesEveryPlayingPlayer()
        {
            using var coordinator = new MediaPlaybackCoordinator();
            var first = new TestPlayer { IsPlayingValue = true };
            var second = new TestPlayer { IsPlayingValue = true };
            coordinator.Register(Guid.NewGuid(), first);
            coordinator.Register(Guid.NewGuid(), second);

            coordinator.PauseAll();

            Assert.False(first.IsPlaying);
            Assert.False(second.IsPlaying);
            Assert.Equal(1, first.PauseCount);
            Assert.Equal(1, second.PauseCount);
        }

        [Fact]
        public void Synchronization_UsesActivePlayerAsMaster()
        {
            using var coordinator = new MediaPlaybackCoordinator();
            var master = new TestPlayer
            {
                IsMediaLoadedValue = true,
                IsPlayingValue = true,
                Position = TimeSpan.FromSeconds(12),
                PlaybackSpeed = 1.5
            };
            var follower = new TestPlayer
            {
                IsMediaLoadedValue = true,
                Position = TimeSpan.FromSeconds(2)
            };
            Guid masterId = Guid.NewGuid();
            coordinator.Register(masterId, master);
            coordinator.Register(Guid.NewGuid(), follower);
            coordinator.Activate(masterId);

            coordinator.IsSynchronizationEnabled = true;

            Assert.Equal(master.Position, follower.Position);
            Assert.True(follower.IsPlaying);
            Assert.Equal(master.PlaybackSpeed, follower.PlaybackSpeed);
            Assert.Equal(1, follower.SeekCount);
            Assert.Equal(1, follower.PlayCount);
        }

        private sealed class TestPlayer: IMediaPlayerService
        {
            private bool _isMuted;
            private TimeSpan _position;

            public event EventHandler? MediaOpened;
            public event EventHandler<string>? MediaFailed
            {
                add { }
                remove { }
            }
            public event EventHandler? MediaEnded
            {
                add { }
                remove { }
            }
            public event PropertyChangedEventHandler? PropertyChanged;

            public object PlayerInstance => this;
            public string? Source { get; private set; }
            public TimeSpan Position
            {
                get => _position;
                set
                {
                    _position = value;
                    OnPropertyChanged();
                }
            }
            public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(10);
            public double Volume { get; set; } = 100;
            public bool IsMuted
            {
                get => _isMuted;
                set
                {
                    if(_isMuted == value)
                        return;
                    _isMuted = value;
                    OnPropertyChanged();
                }
            }
            public bool IsPlaying => IsPlayingValue;
            public bool IsMediaLoaded => IsMediaLoadedValue;
            public double PlaybackSpeed { get; set; } = 1;
            public int CurrentAudioStreamIndex => -1;
            public IReadOnlyList<string> AudioStreams => [];
            public int CurrentSubtitleStreamIndex => -1;
            public IReadOnlyList<string> SubtitleStreams => [];

            public bool IsPlayingValue { get; set; }
            public bool IsMediaLoadedValue { get; set; }
            public int PlayCount { get; private set; }
            public int PauseCount { get; private set; }
            public int SeekCount { get; private set; }

            public Task OpenAsync(
                string source,
                bool autoPlay = true,
                CancellationToken cancellationToken = default)
            {
                Source = source;
                IsMediaLoadedValue = true;
                IsPlayingValue = autoPlay;
                MediaOpened?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            }

            public void Play()
            {
                PlayCount++;
                IsPlayingValue = true;
                OnPropertyChanged(nameof(IsPlaying));
            }

            public void Pause()
            {
                PauseCount++;
                IsPlayingValue = false;
                OnPropertyChanged(nameof(IsPlaying));
            }

            public void Stop()
            {
                IsPlayingValue = false;
                OnPropertyChanged(nameof(IsPlaying));
            }

            public void Seek(TimeSpan position)
            {
                SeekCount++;
                Position = position;
            }

            public void FrameForward() { }
            public void FrameBackward() { }
            public void Dispose() { }

            private void OnPropertyChanged([CallerMemberName] string? name = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

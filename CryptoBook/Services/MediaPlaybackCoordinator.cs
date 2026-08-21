using CryptoBook.Interfaces;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryptoBook.Services
{
    public sealed class MediaPlaybackCoordinator:
        IMediaPlaybackCoordinator
    {
        private static readonly TimeSpan MaximumSynchronizationDrift =
            TimeSpan.FromMilliseconds(250);

        private readonly Dictionary<Guid, PlayerEntry> _players = [];
        private Guid? _activeWindowId;
        private int _nextInstanceNumber;
        private bool _isSynchronizationEnabled;
        private bool _isApplyingState;
        private bool _disposed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsSynchronizationEnabled
        {
            get => _isSynchronizationEnabled;
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if(_isSynchronizationEnabled == value)
                    return;

                _isSynchronizationEnabled = value;
                OnPropertyChanged();
                if(value && TryGetActiveEntry(out var active))
                    SynchronizeFrom(active);
            }
        }

        public int PlayerCount => _players.Count;

        public int Register(Guid windowId, IMediaPlayerService player)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(player);
            if(_players.ContainsKey(windowId))
                throw new InvalidOperationException(
                    $"Media player window '{windowId}' is already registered.");

            var entry = new PlayerEntry(
                windowId,
                ++_nextInstanceNumber,
                player,
                player.IsMuted);
            _players.Add(windowId, entry);
            player.PropertyChanged += OnPlayerPropertyChanged;
            player.MediaOpened += OnMediaOpened;
            ApplyAudioSuppression(entry, suppress: true);
            OnPropertyChanged(nameof(PlayerCount));
            return entry.InstanceNumber;
        }

        public void Unregister(Guid windowId)
        {
            if(!_players.Remove(windowId, out var entry))
                return;

            entry.Player.PropertyChanged -= OnPlayerPropertyChanged;
            entry.Player.MediaOpened -= OnMediaOpened;
            if(_activeWindowId == windowId)
                _activeWindowId = null;
            OnPropertyChanged(nameof(PlayerCount));
        }

        public void Activate(Guid windowId)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if(!_players.TryGetValue(windowId, out var active))
                return;

            _activeWindowId = windowId;
            foreach(var entry in _players.Values)
                ApplyAudioSuppression(entry, entry.WindowId != windowId);

            if(_isSynchronizationEnabled)
                SynchronizeFrom(active);
        }

        public void Deactivate(Guid windowId)
        {
            if(_activeWindowId != windowId ||
               !_players.TryGetValue(windowId, out var entry))
            {
                return;
            }

            _activeWindowId = null;
            ApplyAudioSuppression(entry, suppress: true);
        }

        public void PauseAll()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _isApplyingState = true;
            try
            {
                foreach(var entry in _players.Values)
                {
                    if(entry.Player.IsPlaying)
                        entry.Player.Pause();
                }
            }
            finally
            {
                _isApplyingState = false;
            }
        }

        public void Dispose()
        {
            if(_disposed)
                return;

            _disposed = true;
            foreach(var entry in _players.Values)
            {
                entry.Player.PropertyChanged -= OnPlayerPropertyChanged;
                entry.Player.MediaOpened -= OnMediaOpened;
            }
            _players.Clear();
            _activeWindowId = null;
            GC.SuppressFinalize(this);
        }

        private void OnPlayerPropertyChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(sender is not IMediaPlayerService player ||
               !TryGetEntry(player, out var entry))
            {
                return;
            }

            if(args.PropertyName == nameof(IMediaPlayerService.IsMuted) &&
               !_isApplyingState &&
               !entry.IsAudioSuppressed)
            {
                entry.UserMuted = player.IsMuted;
            }

            if(!_isSynchronizationEnabled ||
               _isApplyingState ||
               _activeWindowId != entry.WindowId ||
               args.PropertyName is not (
                   nameof(IMediaPlayerService.Position) or
                   nameof(IMediaPlayerService.IsPlaying) or
                   nameof(IMediaPlayerService.PlaybackSpeed)))
            {
                return;
            }

            SynchronizeFrom(entry);
        }

        private void OnMediaOpened(object? sender, EventArgs args)
        {
            if(!_isSynchronizationEnabled ||
               sender is not IMediaPlayerService player ||
               !TryGetEntry(player, out var opened))
            {
                return;
            }

            if(TryGetActiveEntry(out var active))
            {
                if(active.WindowId == opened.WindowId)
                    SynchronizeFrom(active);
                else
                    SynchronizePlayer(active.Player, opened.Player);
            }
        }

        private void SynchronizeFrom(PlayerEntry source)
        {
            if(!source.Player.IsMediaLoaded)
                return;

            _isApplyingState = true;
            try
            {
                foreach(var target in _players.Values)
                {
                    if(target.WindowId != source.WindowId &&
                       target.Player.IsMediaLoaded)
                    {
                        SynchronizePlayer(source.Player, target.Player);
                    }
                }
            }
            finally
            {
                _isApplyingState = false;
            }
        }

        private static void SynchronizePlayer(
            IMediaPlayerService source,
            IMediaPlayerService target)
        {
            TimeSpan targetPosition = source.Position > target.Duration
                ? target.Duration
                : source.Position;
            if((targetPosition - target.Position).Duration() >
               MaximumSynchronizationDrift)
            {
                target.Seek(targetPosition);
            }

            if(Math.Abs(source.PlaybackSpeed - target.PlaybackSpeed) > 0.001)
                target.PlaybackSpeed = source.PlaybackSpeed;

            if(source.IsPlaying == target.IsPlaying)
                return;

            if(source.IsPlaying)
                target.Play();
            else
                target.Pause();
        }

        private void ApplyAudioSuppression(PlayerEntry entry, bool suppress)
        {
            if(entry.IsAudioSuppressed == suppress)
                return;

            _isApplyingState = true;
            try
            {
                if(suppress)
                {
                    entry.UserMuted = entry.Player.IsMuted;
                    entry.IsAudioSuppressed = true;
                    entry.Player.IsMuted = true;
                }
                else
                {
                    entry.IsAudioSuppressed = false;
                    entry.Player.IsMuted = entry.UserMuted;
                }
            }
            finally
            {
                _isApplyingState = false;
            }
        }

        private bool TryGetActiveEntry(out PlayerEntry entry)
        {
            if(_activeWindowId is Guid windowId &&
               _players.TryGetValue(windowId, out var active))
            {
                entry = active;
                return true;
            }

            entry = null!;
            return false;
        }

        private bool TryGetEntry(
            IMediaPlayerService player,
            out PlayerEntry entry)
        {
            foreach(var candidate in _players.Values)
            {
                if(ReferenceEquals(candidate.Player, player))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null!;
            return false;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private sealed class PlayerEntry(
            Guid windowId,
            int instanceNumber,
            IMediaPlayerService player,
            bool userMuted)
        {
            public Guid WindowId { get; } = windowId;
            public int InstanceNumber { get; } = instanceNumber;
            public IMediaPlayerService Player { get; } = player;
            public bool UserMuted { get; set; } = userMuted;
            public bool IsAudioSuppressed { get; set; }
        }
    }
}

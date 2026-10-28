using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class DriveMonitoringService:IService, IDriveMonitoringService
    {
        private readonly ISystemItemCreateService _systemItemCreateService;
        private readonly Func<IReadOnlyList<IDriveItem>> _discoverDrives;
        private readonly Func<EventArrivedEventHandler, IDisposable> _subscribeToVolumeChanges;
        private readonly Func<string, CancellationToken, Task<IDriveItem?>> _waitForDriveReady;
        private CancellationTokenSource _cancellationTokenSource = new();
        private readonly object _lifecycleLock = new();
        private readonly object _lock = new();
        private readonly Dictionary<string, long> _driveVersions = new(StringComparer.OrdinalIgnoreCase);
        private IDisposable? _subscription;
        private List<IDriveItem> _currentDrives = [];
        private long _nextVersion;
        private bool _stopped;
        private bool _disposed;

        public event Action<IDriveItem> OnDriveConnected;
        public event Action<string> OnDriveDisconnected;
        public event Action OnDevicesChanged;

        public DriveMonitoringService(ISystemItemCreateService systemItemCreateService)
            : this(systemItemCreateService, null)
        {
        }

        internal DriveMonitoringService(
            ISystemItemCreateService systemItemCreateService,
            Func<IReadOnlyList<IDriveItem>>? discoverDrives,
            Func<EventArrivedEventHandler, IDisposable>? subscribeToVolumeChanges = null,
            Func<string, CancellationToken, Task<IDriveItem?>>? waitForDriveReady = null)
        {
            _systemItemCreateService = systemItemCreateService ?? throw new ArgumentNullException(nameof(systemItemCreateService));
            _discoverDrives = discoverDrives ?? DiscoverWritableDrives;
            _subscribeToVolumeChanges = subscribeToVolumeChanges ??
                (handler => new VolumeChangeSubscription(handler, OnDeviceChangeEvent));
            _waitForDriveReady = waitForDriveReady ?? ((root, token) => WaitForDriveReadyAsync(root, token));
            RefreshCurrentDrives();
        }

        public IReadOnlyList<IDriveItem> GetWritableDrives()
        {
            lock(_lock)
            {
                return _currentDrives.ToArray();
            }
        }

        public void StartMonitoring()
        {
            lock(_lifecycleLock)
            {
                lock(_lock)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if(_subscription is not null)
                        return;
                    if(_cancellationTokenSource.IsCancellationRequested)
                    {
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = new CancellationTokenSource();
                    }
                    _stopped = false;
                }
                _subscription = _subscribeToVolumeChanges(OnVolumeChangeEvent);
            }
        }


        public void StopMonitoring()
        {
            lock(_lifecycleLock)
            {
                lock(_lock)
                {
                    if(_disposed || _stopped)
                        return;
                    _stopped = true;
                    _driveVersions.Clear();
                }
                _cancellationTokenSource.Cancel();
                IDisposable? subscription = _subscription;
                _subscription = null;
                subscription?.Dispose();
            }
        }

        private async void OnVolumeChangeEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                await HandleVolumeChangeAsync(
                    Convert.ToUInt16(e.NewEvent["EventType"]),
                    e.NewEvent["DriveName"]?.ToString());
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception exception)
            {
                // A device/driver failure must not escape an async event handler.
                Trace.TraceWarning("Volume monitoring failed: {0}", exception.GetType().Name);
            }
        }

        private void OnDeviceChangeEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                HandleDeviceChange(Convert.ToUInt16(e.NewEvent["EventType"]));
            }
            catch(Exception exception)
            {
                Trace.TraceWarning("Device monitoring failed: {0}", exception.GetType().Name);
            }
        }

        internal void HandleDeviceChange(ushort eventType)
        {
            if(eventType is not (2 or 3))
                return;
            lock(_lock)
            {
                if(_disposed || _stopped)
                    return;
            }
            OnDevicesChanged?.Invoke();
        }

        internal async Task HandleVolumeChangeAsync(ushort eventType, string? driveName)
        {
            if(eventType != 2 && eventType != 3)
                return;

            if(string.IsNullOrWhiteSpace(driveName))
                return;

            string letter = driveName.Trim().TrimEnd(':', '\\');
            if(letter.Length != 1 || !char.IsAsciiLetter(letter[0]))
                return;
            string root = letter + ":\\";
            long version;
            CancellationToken token;
            lock(_lock)
            {
                if(_disposed || _stopped)
                    return;
                version = ++_nextVersion;
                _driveVersions[root] = version;
                token = _cancellationTokenSource.Token;
                if(eventType == 3)
                    _currentDrives.RemoveAll(d => string.Equals(d.RootDirectory, root, StringComparison.OrdinalIgnoreCase));
            }

            if(eventType == 2)  // Подключение
            {
                IDriveItem? driveEx;
                try
                {
                    driveEx = await _waitForDriveReady(root, token).ConfigureAwait(false);
                }
                catch(OperationCanceledException) when(token.IsCancellationRequested)
                {
                    return;
                }

                if(driveEx != null)
                {
                    lock(_lock)
                    {
                        if(_disposed || _stopped || token.IsCancellationRequested ||
                           !_driveVersions.TryGetValue(root, out long currentVersion) || currentVersion != version ||
                           _currentDrives.Any(d => string.Equals(d.RootDirectory, root, StringComparison.OrdinalIgnoreCase)))
                            return;
                        _currentDrives.Add(driveEx);
                    }
                    OnDriveConnected?.Invoke(driveEx);
                }
            } else if(eventType == 3)  // Отключение
            {
                OnDriveDisconnected?.Invoke(root);
            }
        }

        public void RefreshCurrentDrives()
        {
            lock(_lock)
            {
                _currentDrives = _discoverDrives().ToList();
            }
        }

        private IReadOnlyList<IDriveItem> DiscoverWritableDrives() =>
            DriveInfo.GetDrives()
                .Select(drive => GetDriveIfWritable(drive.Name))
                .OfType<IDriveItem>()
                .ToArray();

        private IDriveItem? GetDriveIfWritable(string root)
        {
            try
            {
                var drive = new DriveInfo(root);
                if(drive.IsReady &&
                    drive.DriveType != DriveType.Network &&
                    drive.DriveType != DriveType.CDRom &&
                    drive.DriveType != DriveType.Unknown &&
                    drive.DriveType != DriveType.NoRootDirectory &&
                    drive.DriveType != DriveType.Ram &&
                    drive.AvailableFreeSpace > 0)
                {
                    return _systemItemCreateService.CreateRoot(drive.Name);
                }
            } catch { /* Игнорируем недоступные диски */ }

            return null;
        }

        private async Task<IDriveItem?> WaitForDriveReadyAsync(string root, CancellationToken ct, int maxAttempts = 15, int delayMs = 200)
        {
            for(int i = 0; i < maxAttempts; i++)
            {
                if(ct.IsCancellationRequested)
                    return null;

                IDriveItem? drive = GetDriveIfWritable(root);
                if(drive is not null)
                    return drive;

                await Task.Delay(delayMs, ct);
            }

            // После всех попыток — диск так и не готов
            return null;
        }

        public void Dispose()
        {
            lock(_lifecycleLock)
            {
                StopMonitoring();
                lock(_lock)
                {
                    if(_disposed)
                        return;
                    _disposed = true;
                    _cancellationTokenSource.Dispose();
                }
            }
        }

        private sealed class VolumeChangeSubscription: IDisposable
        {
            private readonly List<(ManagementEventWatcher Watcher, EventArrivedEventHandler Handler)> _watchers = [];

            public VolumeChangeSubscription(
                EventArrivedEventHandler volumeHandler, EventArrivedEventHandler deviceHandler)
            {
                try
                {
                    Subscribe("SELECT * FROM Win32_VolumeChangeEvent", volumeHandler);
                    Subscribe("SELECT * FROM Win32_DeviceChangeEvent", deviceHandler);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private void Subscribe(string query, EventArrivedEventHandler handler)
            {
                var watcher = new ManagementEventWatcher(new WqlEventQuery(query));
                _watchers.Add((watcher, handler));
                watcher.EventArrived += handler;
                watcher.Start();
            }

            public void Dispose()
            {
                foreach((ManagementEventWatcher watcher, EventArrivedEventHandler handler) in _watchers)
                {
                    watcher.EventArrived -= handler;
                    try
                    {
                        watcher.Stop();
                    }
                    catch(ManagementException exception)
                    {
                        Trace.TraceWarning("Device subscription stop failed: {0}", exception.GetType().Name);
                    }
                    finally
                    {
                        watcher.Dispose();
                    }
                }
                _watchers.Clear();
            }
        }
    }


}

using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class DriveMonitoringService:IService, IDriveMonitoringService
    {
        private readonly ISystemItemCreateService _systemItemCreateService;
        private readonly Func<IReadOnlyList<IDriveItem>> _discoverDrives;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private ManagementEventWatcher? _watcher;
        private readonly object _lock = new object();
        private List<IDriveItem> _currentDrives = [];

        public event Action<IDriveItem> OnDriveConnected;
        public event Action<string> OnDriveDisconnected;

        public DriveMonitoringService(ISystemItemCreateService systemItemCreateService)
            : this(systemItemCreateService, null)
        {
        }

        internal DriveMonitoringService(
            ISystemItemCreateService systemItemCreateService,
            Func<IReadOnlyList<IDriveItem>>? discoverDrives)
        {
            _systemItemCreateService = systemItemCreateService ?? throw new ArgumentNullException(nameof(systemItemCreateService));
            _discoverDrives = discoverDrives ?? DiscoverWritableDrives;
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
            var query = new WqlEventQuery(
            "SELECT * FROM Win32_VolumeChangeEvent");

            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnVolumeChangeEvent;
            _watcher.Start();
        }


        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async void OnVolumeChangeEvent(object sender, EventArrivedEventArgs e)
        {
            ushort eventType = (ushort)e.NewEvent["EventType"];
            try
            {
                await HandleVolumeChangeAsync(eventType, e.NewEvent["DriveName"]?.ToString());
            }
            catch(OperationCanceledException) when(_cancellationTokenSource.IsCancellationRequested)
            {
            }
        }

        internal async Task HandleVolumeChangeAsync(ushort eventType, string? driveName)
        {
            if(eventType != 2 && eventType != 3)
                return;

            if(string.IsNullOrEmpty(driveName))
                return;

            string root = driveName.TrimEnd(':', '\\') + ":\\";

            if(eventType == 2)  // Подключение
            {
                var driveEx = await WaitForDriveReadyAsync(root, _cancellationTokenSource.Token);

                if(driveEx != null)
                {
                    lock(_lock)
                    {
                        if(!_currentDrives.Any(d => string.Equals(d.RootDirectory, driveEx.RootDirectory, StringComparison.OrdinalIgnoreCase)))
                        {
                            _currentDrives.Add(driveEx);
                        }
                    }
                    OnDriveConnected?.Invoke(driveEx);
                }
            } else if(eventType == 3)  // Отключение
            {
                lock(_lock)
                {
                    var removed = _currentDrives.FirstOrDefault(d =>
                        string.Equals(d.RootDirectory, root, StringComparison.OrdinalIgnoreCase));

                    if(removed != null)
                    {
                        _currentDrives.Remove(removed);
                    }
                }

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

                DriveInfo drive;
                try
                {
                    drive = new DriveInfo(root);
                } catch
                {
                    drive = null;
                }

                if(drive?.IsReady == true)
                {
                    return GetDriveIfWritable(drive.Name);  // или напрямую создаём DriveInfoEx
                }

                await Task.Delay(delayMs, ct);
            }

            // После всех попыток — диск так и не готов
            return null;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
          if(_watcher != null)
            {
                _watcher.EventArrived -= OnVolumeChangeEvent;
                _watcher.Stop();
                _watcher.Dispose();
                _watcher = null;
            }
        }
    }


}

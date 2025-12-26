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
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private ManagementEventWatcher? _watcher;
        private readonly object _lock = new object();
        private List<DriveInfoEx> _currentDrives = new List<DriveInfoEx>();

        public event Action<DriveInfoEx> OnDriveConnected;
        public event Action<string> OnDriveDisconnected;

        public DriveMonitoringService()
        {
            RefreshCurrentDrives();
        }

        public IReadOnlyList<DriveInfoEx> GetWritableDrives()
        {
            lock(_lock)
            {
                return _currentDrives.AsReadOnly();
            }
        }

        public async Task StartMonitoringAsync()
        {
            await Task.Run(async () =>
            {
                var query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 OR EventType = 3");
                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += OnVolumeChangeEvent;
                _watcher.Start();

                // Ждём отмены
                while(!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async void OnVolumeChangeEvent(object sender, EventArrivedEventArgs e)
        {
            ushort eventType = (ushort)e.NewEvent["EventType"];
            if(eventType != 2 && eventType != 3)
                return;

            string driveName = e.NewEvent["DriveName"]?.ToString();
            if(string.IsNullOrEmpty(driveName))
                return;

            string root = driveName.EndsWith(":") ? driveName + "\\" : driveName + "\\";

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
                string normalized = root.TrimEnd('\\') + ":";  

                lock(_lock)
                {
                    var removed = _currentDrives.FirstOrDefault(d =>
                        string.Equals(d.RootDirectory.TrimEnd('\\'), normalized, StringComparison.OrdinalIgnoreCase));

                    if(removed != null)
                    {
                        _currentDrives.Remove(removed);
                    }
                }

                OnDriveDisconnected?.Invoke(normalized);
            }
        }

        private void RefreshCurrentDrives()
        {
            lock(_lock)
            {
                _currentDrives.Clear();
                foreach(var drive in DriveInfo.GetDrives())
                {
                    var ex = GetDriveIfWritable(drive.Name);
                    if(ex != null)
                    {
                        _currentDrives.Add(ex);
                    }
                }
            }
        }

        private DriveInfoEx? GetDriveIfWritable(string root)
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
                    return new DriveInfoEx
                    {
                        RootDirectory = drive.Name,
                        VolumeLabel = drive.VolumeLabel,
                        DriveFormat = drive.DriveFormat,
                        DriveType = drive.DriveType,
                        AvailableFreeSpace = drive.AvailableFreeSpace,
                        TotalSize = drive.TotalSize
                    };
                }
            } catch { /* Игнорируем недоступные диски */ }

            return null;
        }

        private async Task<DriveInfoEx?> WaitForDriveReadyAsync(string root, CancellationToken ct, int maxAttempts = 15, int delayMs = 200)
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
            StopMonitoring();
            _cancellationTokenSource?.Dispose();
        }
    }


}

using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CryptoBook.Services
{
    public class DriveManagerService: IDriveManagerService
    {
        private readonly IDriveMonitoringService _monitoringService;
        private readonly ObservableCollection<DriveInfoEx> _writableDrives;

        private readonly IDispatcherService _uiDispatcher;

        public ReadOnlyObservableCollection<DriveInfoEx> WritableDrives { get; }

        public event Action<DriveInfoEx> DriveConnected;
        public event Action<string> DriveDisconnected;

       public DriveManagerService(IDriveMonitoringService monitoringService, IDispatcherService dispatcherService)
        {
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));

            _uiDispatcher = dispatcherService;
            _writableDrives = new ObservableCollection<DriveInfoEx>(_monitoringService.GetWritableDrives());

            WritableDrives = new ReadOnlyObservableCollection<DriveInfoEx>(_writableDrives);

            _monitoringService.OnDriveConnected += OnDriveConnected;
            _monitoringService.OnDriveDisconnected += OnDriveDisconnected;
        }


        public void StartMonitoring()
        {
            _monitoringService.StartMonitoring();
        }
        public void StopMonitoring()
        {
            _monitoringService.StopMonitoring();
        }


        private void OnDriveConnected(DriveInfoEx drive)
        {
            InvokeOnUiThread(() =>
            {
                if(!_writableDrives.Any(d => string.Equals(d.RootDirectory, drive.RootDirectory, StringComparison.OrdinalIgnoreCase)))
                {
                    _writableDrives.Add(drive);
                }

                DriveConnected?.Invoke(drive);
            });
        }

        private void OnDriveDisconnected(string driveName)
        {
            InvokeOnUiThread(() =>
            {
                var driveToRemove = _writableDrives.FirstOrDefault(d =>
                    string.Equals(d.RootDirectory.TrimEnd('\\'), driveName.TrimEnd(':'), StringComparison.OrdinalIgnoreCase));

                if(driveToRemove != null)
                {
                    _writableDrives.Remove(driveToRemove);
                }

                DriveDisconnected?.Invoke(driveName);
            });
        }

        private void InvokeOnUiThread(Action action)
        {
            if(_uiDispatcher.CheckAccess())
            {
                action();
            } else
            {
                _uiDispatcher.Invoke(action);
            }
        }


        public void Dispose()
        {
            _monitoringService.OnDriveConnected -= OnDriveConnected;
            _monitoringService.OnDriveDisconnected -= OnDriveDisconnected;
            _monitoringService.Dispose();
        }
    }
}

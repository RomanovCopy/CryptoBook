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
        private readonly ObservableCollection<IDriveItem> _writableDrives;

        private readonly IDispatcherService _uiDispatcher;
        private readonly IStorageFacade? _storage;
        private readonly ISystemItemCreateService? _itemFactory;
        private readonly CancellationTokenSource _portableMonitoringCancellation = new();
        private Task? _portableMonitoringTask;

        public ReadOnlyObservableCollection<IDriveItem> WritableDrives { get; }

        public event Action<IDriveItem> DriveConnected;
        public event Action<string> DriveDisconnected;

       public DriveManagerService(
           IDriveMonitoringService monitoringService,
           IDispatcherService dispatcherService,
           IStorageFacade? storage = null,
           ISystemItemCreateService? itemFactory = null)
        {
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));

            _uiDispatcher = dispatcherService;
            _storage = storage;
            _itemFactory = itemFactory;
            _writableDrives = new ObservableCollection<IDriveItem>(_monitoringService.GetWritableDrives());

            WritableDrives = new ReadOnlyObservableCollection<IDriveItem>(_writableDrives);

            _monitoringService.OnDriveConnected += OnDriveConnected;
            _monitoringService.OnDriveDisconnected += OnDriveDisconnected;
        }


        public void StartMonitoring()
        {
            _monitoringService.StartMonitoring();
            if(_storage is not null && _itemFactory is not null && _portableMonitoringTask is null)
            {
                _portableMonitoringTask = MonitorPortableRootsAsync(
                    _portableMonitoringCancellation.Token);
            }
        }
        public void StopMonitoring()
        {
            _monitoringService.StopMonitoring();
            _portableMonitoringCancellation.Cancel();
        }

        private async Task MonitorPortableRootsAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshPortableRootsAsync(cancellationToken);
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    // A missing optional transport must not affect local drives.
                }
                try
                {
                    if(!await timer.WaitForNextTickAsync(cancellationToken))
                        return;
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private async Task RefreshPortableRootsAsync(CancellationToken cancellationToken)
        {
            if(_storage is null || _itemFactory is null)
                return;

            StorageItemMetadata[] roots = (await _storage.GetRootsAsync(cancellationToken))
                .Where(root => !root.Location.IsLocal)
                .ToArray();
            await _uiDispatcher.InvokeAsync(new Action(() =>
            {
                var incoming = roots.ToDictionary(
                    root => root.Location.ToString(),
                    StringComparer.OrdinalIgnoreCase);
                for(int index = _writableDrives.Count - 1; index >= 0; index--)
                {
                    IDriveItem existing = _writableDrives[index];
                    if(existing.Location.IsLocal || incoming.ContainsKey(existing.FullPath))
                        continue;
                    _writableDrives.RemoveAt(index);
                    DriveDisconnected?.Invoke(existing.FullPath);
                }

                foreach(StorageItemMetadata metadata in roots)
                {
                    IDriveItem? existing = _writableDrives.FirstOrDefault(root =>
                        string.Equals(root.FullPath, metadata.Location.ToString(), StringComparison.OrdinalIgnoreCase));
                    if(existing is null)
                    {
                        IDriveItem root = _itemFactory.CreateRoot(metadata);
                        _writableDrives.Add(root);
                        DriveConnected?.Invoke(root);
                    }
                    else
                    {
                        existing.Name = metadata.Name;
                        existing.Capabilities = metadata.Capabilities;
                        existing.StatusText = metadata.StatusText;
                        existing.DriveFormat =
                            $"{metadata.Location.ProviderId.ToUpperInvariant()} • {metadata.StatusText}";
                    }
                }
            }));
        }


        private void OnDriveConnected(IDriveItem drive)
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
                var driveToRemove = _writableDrives.FirstOrDefault(d => string.Equals(d.RootDirectory,
                    driveName.TrimEnd(':', '\\') + ":\\", StringComparison.OrdinalIgnoreCase));

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
            _portableMonitoringCancellation.Cancel();
            _portableMonitoringCancellation.Dispose();
            _monitoringService.OnDriveConnected -= OnDriveConnected;
            _monitoringService.OnDriveDisconnected -= OnDriveDisconnected;
        }
    }
}

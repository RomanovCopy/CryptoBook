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
        private readonly object _monitoringLock = new();
        private CancellationTokenSource? _portableMonitoringCancellation;
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private readonly SemaphoreSlim _portableRefreshSignal = new(0, 1);
        private Task? _portableMonitoringTask;
        private volatile bool _isMonitoring;
        private volatile bool _disposed;

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
            _monitoringService.OnDevicesChanged += OnDevicesChanged;
        }


        public void StartMonitoring()
        {
            lock(_monitoringLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if(_isMonitoring)
                    return;
                _monitoringService.StartMonitoring();
                _isMonitoring = true;
                if(_storage is not null && _itemFactory is not null)
                {
                    _portableMonitoringCancellation = new CancellationTokenSource();
                    _portableMonitoringTask = MonitorPortableRootsAsync(
                        _portableMonitoringCancellation.Token);
                }
            }
        }
        public void StopMonitoring()
        {
            lock(_monitoringLock)
            {
                if(!_isMonitoring)
                    return;
                _isMonitoring = false;
                try
                {
                    _monitoringService.StopMonitoring();
                }
                finally
                {
                    CancellationTokenSource? cancellation = _portableMonitoringCancellation;
                    Task? monitoring = _portableMonitoringTask;
                    _portableMonitoringCancellation = null;
                    _portableMonitoringTask = null;
                    if(cancellation is not null)
                    {
                        cancellation.Cancel();
                        _ = DisposeMonitoringCancellationAsync(monitoring!, cancellation);
                    }
                }
            }
        }

        private static async Task DisposeMonitoringCancellationAsync(
            Task monitoring, CancellationTokenSource cancellation)
        {
            try
            {
                await monitoring.ConfigureAwait(false);
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _refreshGate.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(_monitoringService.RefreshCurrentDrives, cancellationToken);
                await _uiDispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    RefreshLocalRoots(_monitoringService.GetWritableDrives());
                });
                try
                {
                    await RefreshPortableRootsCoreAsync(cancellationToken);
                }
                catch(Exception exception) when(exception is not OperationCanceledException)
                {
                    // An unavailable optional transport must not block opening local folders.
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        private void RefreshLocalRoots(IReadOnlyList<IDriveItem> roots)
        {
            var incoming = roots.ToDictionary(
                root => root.RootDirectory,
                StringComparer.OrdinalIgnoreCase);
            for(int index = _writableDrives.Count - 1; index >= 0; index--)
            {
                IDriveItem existing = _writableDrives[index];
                if(!existing.Location.IsLocal || incoming.ContainsKey(existing.RootDirectory))
                    continue;
                _writableDrives.RemoveAt(index);
                DriveDisconnected?.Invoke(existing.RootDirectory);
            }

            foreach(IDriveItem root in roots)
            {
                if(_writableDrives.Any(existing => existing.Location.IsLocal &&
                    string.Equals(existing.RootDirectory, root.RootDirectory, StringComparison.OrdinalIgnoreCase)))
                    continue;
                _writableDrives.Add(root);
                DriveConnected?.Invoke(root);
            }
        }

        private async Task MonitorPortableRootsAsync(CancellationToken cancellationToken)
        {
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
                    await _portableRefreshSignal.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private void OnDevicesChanged()
        {
            if(!_isMonitoring || _disposed)
                return;
            try
            {
                // Coalesce bursts of Windows notifications and wake discovery immediately.
                _portableRefreshSignal.Release();
            }
            catch(SemaphoreFullException)
            {
            }
        }

        private async Task RefreshPortableRootsAsync(CancellationToken cancellationToken)
        {
            await _refreshGate.WaitAsync(cancellationToken);
            try
            {
                await RefreshPortableRootsCoreAsync(cancellationToken);
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        private async Task RefreshPortableRootsCoreAsync(CancellationToken cancellationToken)
        {
            if(_storage is null || _itemFactory is null)
                return;

            StorageItemMetadata[] roots = (await _storage.GetRootsAsync(cancellationToken))
                .Where(root => !root.Location.IsLocal &&
                    !string.Equals(root.StatusText, "offline", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(root.StatusText, "disconnected", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            await _uiDispatcher.InvokeAsync(new Action(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(_disposed, this);
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
            InvokeOnUiThread(() => RefreshLocalRoots(_monitoringService.GetWritableDrives()));
        }

        private void OnDriveDisconnected(string driveName)
        {
            InvokeOnUiThread(() => RefreshLocalRoots(_monitoringService.GetWritableDrives()));
        }

        private void InvokeOnUiThread(Action action)
        {
            void ApplyIfActive()
            {
                if(!_disposed)
                    action();
            }
            if(_uiDispatcher.CheckAccess())
            {
                ApplyIfActive();
            }
            else
            {
                // WMI shutdown can wait for callbacks; never block them on the UI thread.
                _uiDispatcher.BeginInvoke(ApplyIfActive);
            }
        }


        public void Dispose()
        {
            lock(_monitoringLock)
            {
                if(_disposed)
                    return;
                StopMonitoring();
                _disposed = true;
                _monitoringService.OnDriveConnected -= OnDriveConnected;
                _monitoringService.OnDriveDisconnected -= OnDriveDisconnected;
                _monitoringService.OnDevicesChanged -= OnDevicesChanged;
            }
        }
    }
}

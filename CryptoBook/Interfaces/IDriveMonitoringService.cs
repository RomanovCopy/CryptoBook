using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDriveMonitoringService: IService, IDisposable
    {
        IReadOnlyList<IDriveItem> GetWritableDrives();

        event Action<IDriveItem> OnDriveConnected;
        event Action<string> OnDriveDisconnected;  // Только RootDirectory, т.к. после отключения DriveInfo недоступен

        void StartMonitoring();
        void StopMonitoring();
    }
}

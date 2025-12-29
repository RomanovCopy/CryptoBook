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
        IReadOnlyList<DriveInfoEx> GetWritableDrives();

        event Action<DriveInfoEx> OnDriveConnected;
        event Action<string> OnDriveDisconnected;  // Только RootDirectory, т.к. после отключения DriveInfo недоступен

        void StartMonitoring();
        void StopMonitoring();
    }
}

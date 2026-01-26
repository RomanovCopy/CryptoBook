using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDirectoryMonitoringService:IDisposable
    {
        bool StartMonitoring(string directoryPath, Action<FileSystemEventArgs>? onCreated,Action<FileSystemEventArgs>? onDeleted,Action<RenamedEventArgs>? onRenamed);
        bool StopMonitoring(string directoryPath);
    }
}

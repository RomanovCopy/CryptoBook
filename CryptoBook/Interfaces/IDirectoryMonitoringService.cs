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
        public bool StartMonitoring(
       string directoryPath,
       Action<FileSystemEventArgs>? onCreated = null,
       Action<FileSystemEventArgs>? onDeleted = null,
       Action<RenamedEventArgs>? onRenamed = null,
       Action<FileSystemEventArgs>? onChanged = null,
       Action<Exception?>? onOverflowOrError = null,
       bool includeSubdirectories = false,
       NotifyFilters notifyFilters = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
       int internalBufferSize = 64 * 1024);

        bool StopMonitoring(string directoryPath);
    }
}

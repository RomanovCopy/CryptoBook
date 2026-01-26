using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class DirectoryMonitoringService: IDirectoryMonitoringService
    {
        private readonly Dictionary<string, WatcherEntry> _watchers =
        new(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new();

        public bool StartMonitoring(
            string directoryPath,
            Action<FileSystemEventArgs>? onCreated = null,
            Action<FileSystemEventArgs>? onDeleted = null,
            Action<RenamedEventArgs>? onRenamed = null)
        {
            if(string.IsNullOrWhiteSpace(directoryPath))
                return false;

            if(!Directory.Exists(directoryPath))
                return false;

            var key = NormalizePath(directoryPath);

            lock(_lock)
            {
                if(_watchers.ContainsKey(key))
                    return false;

                var watcher = new FileSystemWatcher(directoryPath)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,

                    InternalBufferSize = 64 * 1024, // максимум 64KB
                    EnableRaisingEvents = false
                };

                FileSystemEventHandler? createdHandler = null;
                FileSystemEventHandler? deletedHandler = null;
                RenamedEventHandler? renamedHandler = null;
                ErrorEventHandler? errorHandler = null;

                if(onCreated != null)
                {
                    createdHandler = (_, e) =>
                    {
                        try
                        { onCreated(e); } catch { /* лог по желанию */ }
                    };
                    watcher.Created += createdHandler;
                }

                if(onDeleted != null)
                {
                    deletedHandler = (_, e) =>
                    {
                        try
                        { onDeleted(e); } catch { }
                    };
                    watcher.Deleted += deletedHandler;
                }

                if(onRenamed != null)
                {
                    renamedHandler = (_, e) =>
                    {
                        try
                        { onRenamed(e); } catch { }
                    };
                    watcher.Renamed += renamedHandler;
                }

                // Важно: ловим ошибки и переполнение буфера
                errorHandler = (_, e) =>
                {
                    // e.GetException() часто InternalBufferOverflowException или IO/Win32 ошибки
                    // Тут обычно: лог + можно перезапустить watcher
                    // Например: Restart(key) или сигнал наружу
                };
                watcher.Error += errorHandler;

                _watchers[key] = new WatcherEntry(
                    watcher,
                    createdHandler,
                    deletedHandler,
                    renamedHandler,
                    errorHandler);

                watcher.EnableRaisingEvents = true;
                return true;
            }
        }

        public bool StopMonitoring(string directoryPath)
        {
            if(string.IsNullOrWhiteSpace(directoryPath))
                return false;

            var key = NormalizePath(directoryPath);

            WatcherEntry? entry;

            lock(_lock)
            {
                if(!_watchers.TryGetValue(key, out entry))
                    return false;

                _watchers.Remove(key);
            }

            // Dispose вне lock, чтобы не блокировать другие операции
            entry.Dispose();
            return true;
        }

        public void Dispose()
        {
            WatcherEntry[] entries;

            lock(_lock)
            {
                entries = new WatcherEntry[_watchers.Count];
                _watchers.Values.CopyTo(entries, 0);
                _watchers.Clear();
            }

            foreach(var e in entries)
                e.Dispose();
        }

        private static string NormalizePath(string path)
        {
            var full = Path.GetFullPath(path);
            full = Path.TrimEndingDirectorySeparator(full);
            return full;
        }

        private sealed class WatcherEntry: IDisposable
        {
            private readonly FileSystemWatcher _watcher;
            private readonly FileSystemEventHandler? _created;
            private readonly FileSystemEventHandler? _deleted;
            private readonly RenamedEventHandler? _renamed;
            private readonly ErrorEventHandler? _error;

            public WatcherEntry(
                FileSystemWatcher watcher,
                FileSystemEventHandler? created,
                FileSystemEventHandler? deleted,
                RenamedEventHandler? renamed,
                ErrorEventHandler? error)
            {
                _watcher = watcher;
                _created = created;
                _deleted = deleted;
                _renamed = renamed;
                _error = error;
            }

            public void Dispose()
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;

                    if(_created != null)
                        _watcher.Created -= _created;
                    if(_deleted != null)
                        _watcher.Deleted -= _deleted;
                    if(_renamed != null)
                        _watcher.Renamed -= _renamed;
                    if(_error != null)
                        _watcher.Error -= _error;

                    _watcher.Dispose();
                } catch { }
            }
        }
    }
}

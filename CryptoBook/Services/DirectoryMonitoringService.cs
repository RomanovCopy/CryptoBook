using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Надёжный менеджер FileSystemWatcher'ов:
/// - нормализация путей (корни дисков/UNC не превращаются в "C:")
/// - безопасный Stop/Dispose без гонок с перезапуском
/// - debounce перезапуска при Error/overflow (не плодит Task.Run)
/// - опционально: Changed
/// - опционально: callback OnOverflow, чтобы инициировать “полный рескан” каталога
/// </summary>
public sealed class DirectoryMonitoringService: IDirectoryMonitoringService, IDisposable
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();

    // Параметры перезапуска
    private readonly TimeSpan _restartDebounce = TimeSpan.FromMilliseconds(350);
    private readonly int _restartMaxAttempts = 5;
    private readonly TimeSpan _restartBaseDelay = TimeSpan.FromMilliseconds(150);

    public bool StartMonitoring(
        string directoryPath,
        Action<FileSystemEventArgs>? onCreated = null,
        Action<FileSystemEventArgs>? onDeleted = null,
        Action<RenamedEventArgs>? onRenamed = null,
        Action<FileSystemEventArgs>? onChanged = null,
        Action<Exception?>? onOverflowOrError = null,
        bool includeSubdirectories = false,
        NotifyFilters notifyFilters = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
        int internalBufferSize = 64 * 1024)
    {
        if(string.IsNullOrWhiteSpace(directoryPath))
            return false;

        string key;
        try
        {
            key = NormalizePath(directoryPath);
        } catch
        {
            return false;
        }

        if(!Directory.Exists(key))
            return false;

        lock(_lock)
        {
            if(!_entries.TryGetValue(key, out var entry))
            {

                FileSystemWatcher? watcher = null;
                try
                {
                    watcher = CreateWatcher(key, includeSubdirectories, notifyFilters, internalBufferSize);

                    // handlers (держим ссылки, чтобы уметь отписаться)
                    FileSystemEventHandler? createdHandler = null;
                    FileSystemEventHandler? deletedHandler = null;
                    RenamedEventHandler? renamedHandler = null;
                    FileSystemEventHandler? changedHandler = null;
                    ErrorEventHandler? errorHandler = null;

                    if(onCreated != null)
                    {
                        createdHandler = (_, e) => SafeInvoke(() => onCreated(e));
                        watcher.Created += createdHandler;
                    }

                    if(onDeleted != null)
                    {
                        deletedHandler = (_, e) => SafeInvoke(() => onDeleted(e));
                        watcher.Deleted += deletedHandler;
                    }

                    if(onRenamed != null)
                    {
                        renamedHandler = (_, e) => SafeInvoke(() => onRenamed(e));
                        watcher.Renamed += renamedHandler;
                    }

                    if(onChanged != null)
                    {
                        // Важно: Changed бывает “шумным”, пользователь сам решает что с этим делать.
                        changedHandler = (_, e) => SafeInvoke(() => onChanged(e));
                        watcher.Changed += changedHandler;
                    }

                    // Entry заранее создаём, чтобы errorHandler мог дергать entry.ScheduleRestart()
                    WatcherEntry? watcherEntry = null;

                    errorHandler = (_, e) =>
                    {
                        try
                        {
                            var ex = e.GetException();
                            // overflow / IO / Win32 — считаем “перезапускаем и просим рескан”
                            if(ex is InternalBufferOverflowException ||
                                ex is IOException ||
                                ex is Win32Exception)
                            {
                                SafeInvoke(() => onOverflowOrError?.Invoke(ex));
                                watcherEntry?.ScheduleRestart(ex);
                            } else
                            {
                                // прочие ошибки — тоже сообщим, но перезапуск можно оставить на усмотрение
                                SafeInvoke(() => onOverflowOrError?.Invoke(ex));
                                watcherEntry?.ScheduleRestart(ex);
                            }
                        } catch
                        {
                            // НИКОГДА не бросаем из обработчика Error
                        }
                    };
                    watcher.Error += errorHandler;
                    watcher.EnableRaisingEvents = true;
                    entry=new Entry(key, watcher);

                    _entries[key] = entry;

                    return true;
                } catch
                {
                    try
                    { watcher?.Dispose(); } catch { }
                    return false;
                }
            } else
            {                 
                entry.RefCount++;
                return true;
            }
        }
    }

    public bool StopMonitoring(string directoryPath)
    {
        if(string.IsNullOrWhiteSpace(directoryPath))
            return false;

        string key;
        try
        {
            key = NormalizePath(directoryPath);
        } catch
        {
            return false;
        }

        Entry? entry;
        lock(_lock)
        {
            if(!_entries.TryGetValue(key, out entry))
                return false;
            else
            {
                entry.RefCount--;
                if(entry.RefCount > 0)
                    return true; // кто-то ещё держит ссылку, не удаляем
            }
            _entries.Remove(key);
        }
        entry.Dispose();
        return true;
    }

    public void Dispose()
    {
        Entry[] entries;
        lock(_lock)
        {
            entries = new Entry[_entries.Count];
            _entries.Values.CopyTo(entries, 0);
            _entries.Clear();
        }

        foreach(var e in entries)
            e.Dispose();
    }

    private static FileSystemWatcher CreateWatcher(
        string path,
        bool includeSubdirectories,
        NotifyFilters notifyFilters,
        int internalBufferSize)
    {
        // Важно: InternalBufferSize в .NET ограничен, но setter может бросить исключение.
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = notifyFilters,
            EnableRaisingEvents = false
        };

        if(internalBufferSize > 0)
        {
            // FileSystemWatcher допускает 4..64KB по докам/практике, но Windows может вести себя по-разному.
            // Мы пробуем установить, а если нельзя — просто игнорируем.
            try
            { watcher.InternalBufferSize = Math.Min(internalBufferSize, 64 * 1024); } catch { /* ignore */ }
        }

        return watcher;
    }

    private static void SafeInvoke(Action action)
    {
        try
        { action(); } catch { }
    }

    private static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);

        if(!string.IsNullOrEmpty(root))
        {
            // Если это ровно корень (C:\, \\server\share\) — возвращаем корень с разделителем
            var fullTrim = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rootTrim = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if(string.Equals(fullTrim, rootTrim, StringComparison.OrdinalIgnoreCase))
                return rootTrim + Path.DirectorySeparatorChar;
        }

        // Иначе убираем хвостовой слэш
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class WatcherEntry: IDisposable
    {
        private readonly string _key;
        private readonly bool _includeSubdirs;
        private readonly NotifyFilters _notifyFilters;
        private readonly int _internalBufferSize;

        private readonly TimeSpan _restartDebounce;
        private readonly int _restartMaxAttempts;
        private readonly TimeSpan _restartBaseDelay;

        private FileSystemWatcher _watcher;

        private readonly FileSystemEventHandler? _created;
        private readonly FileSystemEventHandler? _deleted;
        private readonly RenamedEventHandler? _renamed;
        private readonly FileSystemEventHandler? _changed;
        private readonly ErrorEventHandler? _error;

        private int _disposed;               // 0/1
        private int _restartInProgress;      // 0/1

        // Debounce restart
        private readonly object _restartLock = new();
        private CancellationTokenSource? _restartCts;
        private Exception? _lastRestartReason;

        public WatcherEntry(
            string key,
            FileSystemWatcher watcher,
            bool includeSubdirs,
            NotifyFilters notifyFilters,
            int internalBufferSize,
            TimeSpan restartDebounce,
            int restartMaxAttempts,
            TimeSpan restartBaseDelay,
            FileSystemEventHandler? created,
            FileSystemEventHandler? deleted,
            RenamedEventHandler? renamed,
            FileSystemEventHandler? changed,
            ErrorEventHandler? error)
        {
            _key = key;
            _watcher = watcher;

            _includeSubdirs = includeSubdirs;
            _notifyFilters = notifyFilters;
            _internalBufferSize = internalBufferSize;

            _restartDebounce = restartDebounce;
            _restartMaxAttempts = restartMaxAttempts;
            _restartBaseDelay = restartBaseDelay;

            _created = created;
            _deleted = deleted;
            _renamed = renamed;
            _changed = changed;
            _error = error;
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        public void ScheduleRestart(Exception? reason)
        {
            if(IsDisposed)
                return;

            lock(_restartLock)
            {
                if(IsDisposed)
                    return;

                _lastRestartReason = reason;

                _restartCts?.Cancel();
                _restartCts?.Dispose();
                _restartCts = new CancellationTokenSource();

                var token = _restartCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(_restartDebounce, token).ConfigureAwait(false);
                        await RestartAsync(token).ConfigureAwait(false);
                    } catch(OperationCanceledException) { } catch
                    {
                        // Лог по желанию
                    }
                });
            }
        }

        private async Task RestartAsync(CancellationToken token)
        {
            if(IsDisposed)
                return;

            // Не даём параллельных рестартов
            if(Interlocked.Exchange(ref _restartInProgress, 1) == 1)
                return;

            try
            {
                if(IsDisposed)
                    return;

                for(int attempt = 1; attempt <= _restartMaxAttempts; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    if(IsDisposed)
                        return;

                    try
                    {
                        var oldWatcher = _watcher;

                        var newWatcher = new FileSystemWatcher(_key)
                        {
                            IncludeSubdirectories = _includeSubdirs,
                            NotifyFilter = _notifyFilters,
                            EnableRaisingEvents = false
                        };

                        if(_internalBufferSize > 0)
                        {
                            try
                            { newWatcher.InternalBufferSize = Math.Min(_internalBufferSize, 64 * 1024); } catch { /* ignore */ }
                        }

                        if(_created != null)
                            newWatcher.Created += _created;
                        if(_deleted != null)
                            newWatcher.Deleted += _deleted;
                        if(_renamed != null)
                            newWatcher.Renamed += _renamed;
                        if(_changed != null)
                            newWatcher.Changed += _changed;
                        if(_error != null)
                            newWatcher.Error += _error;

                        newWatcher.EnableRaisingEvents = true;

                        if(IsDisposed)
                        {
                            try
                            { newWatcher.EnableRaisingEvents = false; } catch { }
                            try
                            { newWatcher.Dispose(); } catch { }
                            return;
                        }

                        _watcher = newWatcher;

                        // Чистим старый
                        try
                        { oldWatcher.EnableRaisingEvents = false; } catch { }
                        try
                        { if(_created != null) oldWatcher.Created -= _created; } catch { }
                        try
                        { if(_deleted != null) oldWatcher.Deleted -= _deleted; } catch { }
                        try
                        { if(_renamed != null) oldWatcher.Renamed -= _renamed; } catch { }
                        try
                        { if(_changed != null) oldWatcher.Changed -= _changed; } catch { }
                        try
                        { if(_error != null) oldWatcher.Error -= _error; } catch { }
                        try
                        { oldWatcher.Dispose(); } catch { }

                        return; // успех
                    } catch
                    {
                        // экспоненциальный бэкофф (async)
                        var delay = TimeSpan.FromMilliseconds(_restartBaseDelay.TotalMilliseconds * attempt);
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }
                }
            } finally
            {
                Interlocked.Exchange(ref _restartInProgress, 0);
            }
        }

        public void Dispose()
        {
            if(Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            lock(_restartLock)
            {
                try
                { _restartCts?.Cancel(); } catch { }
                try
                { _restartCts?.Dispose(); } catch { }
                _restartCts = null;
            }

            try
            {
                var w = _watcher;
                try
                { w.EnableRaisingEvents = false; } catch { }

                try
                { if(_created != null) w.Created -= _created; } catch { }
                try
                { if(_deleted != null) w.Deleted -= _deleted; } catch { }
                try
                { if(_renamed != null) w.Renamed -= _renamed; } catch { }
                try
                { if(_changed != null) w.Changed -= _changed; } catch { }
                try
                { if(_error != null) w.Error -= _error; } catch { }

                try
                { w.Dispose(); } catch { }
            } catch { }
        }
    }


    private sealed class Entry: IDisposable
    {
        public string Path { get; }
        public FileSystemWatcher Watcher { get; }
        public int RefCount;

        public Entry(string path, FileSystemWatcher watcher)
        {
            Path = path;
            Watcher = watcher;
            RefCount = 0;
        }

        public void Dispose()
        {
            try
            {
                Watcher.EnableRaisingEvents = false;
                Watcher.Dispose();
            } catch { }
        }
    }
}


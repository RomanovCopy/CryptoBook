using CryptoBook.Comparers;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public abstract class ContainerSystemItem: ViewModelBase, IContainerSystemItem
    {
        private readonly IDispatcherService _dispatcherService;
        private readonly IDirectoryMonitoringService _directoryMonitoringService;
        private readonly ISystemItemCreateService _systemItemCreateService;
        private readonly ISystemItemSortService _systemItemSortService;
        private readonly object _monitorEventLock = new();
        private readonly List<(string Path, string? OldPath)> _monitorEvents = [];
        private TaskCompletionSource? _monitorCompletion;

        public string Name { get => name; set => SetProperty(ref name, value); }
        string name;
        public string RootDirectory { get => _rootDirectory; set => SetProperty(ref _rootDirectory, value); }
        string _rootDirectory;
        /// <summary>
        /// полный путь к директории
        /// </summary>
        public string FullPath { get => fullPath; set => SetProperty(ref fullPath, value); }
        string fullPath;
        public string DisplayPath
        {
            get => string.IsNullOrWhiteSpace(displayPath) ? FullPath : displayPath;
            set => SetProperty(ref displayPath, value);
        }
        string displayPath = string.Empty;
        public StorageLocation Location
        {
            get => StorageLocation.Parse(FullPath);
            set => FullPath = value.ToString();
        }
        public StorageProviderCapabilities Capabilities { get => capabilities; set => SetProperty(ref capabilities, value); }
        StorageProviderCapabilities capabilities;
        public string? StatusText { get => statusText; set => SetProperty(ref statusText, value); }
        string? statusText;
        /// <summary>
        /// родительская директория(если лежит на диске то null)
        /// </summary>
        public ISystemItem? Parent { get => parent; set => SetProperty(ref parent, value); }
        ISystemItem? parent;
        /// <summary>
        /// флаг - дочерние элементы загружены
        /// </summary>
        public bool IsLoaded { get => isLoaded; set => SetProperty(ref isLoaded, value); }
        bool isLoaded;

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                SetProperty(ref isExpanded, value);
                if(value)
                {
                    StartMonitoring();
                } else
                {
                    StopMonitoring();
                }
            }
        }
        bool isExpanded;

        public bool IsEditing { get => isEditing; set => SetProperty(ref isEditing, value); }
        bool isEditing;


        public bool IsSelected
        {
            get => isSelected;
            set
            {
                SetProperty(ref isSelected, value);
                if(value)
                {
                    StartMonitoring();
                } else
                {
                    StopMonitoring();
                }
            }
        }
        bool isSelected;

        private bool _monitor;// защита от двойной подписки

        public long Size { get => _size; set => SetProperty(ref _size, _children.Sum(x => x.Size)); }
        long _size;


        /// <summary>
        /// Возвращает доступную только для чтения наблюдаемую коллекцию дочерних элементов,
        /// содержащихся в этом элементе файловой системы.
        /// </summary>
        /// <remarks>Коллекция отражает текущий набор дочерних элементов и уведомляет наблюдателей об изменениях,
        /// таких как добавление или удаление. Коллекция пуста, если у элемента нет дочерних элементов.</remarks>
        public ReadOnlyObservableCollection<ISystemItem> Children { get; private set; }
        protected RangeObservableCollection<ISystemItem> _children;

        /// <summary>
        /// Дочерние каталоги для иерархической навигации.
        /// </summary>
        /// <remarks>
        /// TreeView не должен создавать скрытые контейнеры для каждого файла из <see cref="Children"/>.
        /// </remarks>
        public ReadOnlyObservableCollection<IContainerSystemItem> DirectoryChildren { get; private set; }
        private readonly RangeObservableCollection<IContainerSystemItem> _directoryChildren;

        public DateTime LastWriteTimeUtc { get => lastWriteTimeUtc; set => SetProperty(ref lastWriteTimeUtc, value); }
        DateTime lastWriteTimeUtc;

        protected ContainerSystemItem(IDispatcherService dispatcherService, IDirectoryMonitoringService directoryMonitoringService, ISystemItemCreateService systemItemCreateService, ISystemItemSortService systemItemSortService)
        {
            _dispatcherService = dispatcherService;
            _directoryMonitoringService = directoryMonitoringService;
            _systemItemCreateService = systemItemCreateService;
            _systemItemSortService = systemItemSortService;
            _children = [];
            _directoryChildren = [];
            Children = new ReadOnlyObservableCollection<ISystemItem>(_children);
            DirectoryChildren = new ReadOnlyObservableCollection<IContainerSystemItem>(_directoryChildren);
        }


        public async virtual Task<FileOperationResult> AddChildAsync(IEnumerable<ISystemItem> items,
        Func<ISystemItem, string> keySelector, CancellationToken ct = default)
        {
            if(items is null)
                return FileOperationResult.Fail("Items is null");

            if(keySelector is null)
                return FileOperationResult.Fail("Key selector is null");

            ct.ThrowIfCancellationRequested();

            bool duplicateFound = false;
            ISystemItem[] incoming = items.ToArray();
            // Индекс ключей устраняет повторный линейный поиск для каждого элемента.
            await _dispatcherService.InvokeAsync(() =>
            {
                var keys = new HashSet<string>(
                    _children.Select(keySelector),
                    StringComparer.OrdinalIgnoreCase);

                if(incoming.Length == 1)
                {
                    ISystemItem item = incoming[0];
                    if(!keys.Add(keySelector(item)))
                    {
                        duplicateFound = true;
                        return;
                    }

                    // Одиночное событие FileSystemWatcher не должно сбрасывать
                    // весь ListView и тем самым уничтожать текущее выделение.
                    _children.Add(item);
                    if(item is IContainerSystemItem directory)
                        _directoryChildren.Add(directory);
                    return;
                }

                var combined = new List<ISystemItem>(
                    _children.Count + incoming.Length);
                combined.AddRange(_children);

                foreach(var item in incoming)
                {
                    string key = keySelector(item);
                    if(!keys.Add(key))
                    {
                        duplicateFound = true;
                        continue;
                    }

                    combined.Add(item);
                }

                if(combined.Count != _children.Count)
                    ReplaceChildren(combined);
            });
            return duplicateFound
                ? FileOperationResult.Fail("Item already exists")
                : FileOperationResult.Ok();
        }

        public async virtual Task<FileOperationResult> RenameChildAsync(ISystemItem item, string newName, CancellationToken ct = default)
        {
            if(item is null)
                return FileOperationResult.Fail("Items is null");
            ct.ThrowIfCancellationRequested();
            bool renamed = false;
            ISystemItem existing = _children.FirstOrDefault(c => ReferenceEquals(c, item));
            existing ??= _children.FirstOrDefault(c => string.Equals(c.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase));
            if(existing is not null)
            {
                await _dispatcherService.InvokeAsync(() =>
                {
                    existing.Name = newName;
                    existing.FullPath = Path.Combine(Path.GetDirectoryName(existing.FullPath) ?? string.Empty, newName);
                    existing.DisplayPath = existing.FullPath;
                });
                if(item is IContainerSystemItem container)
                {
                    if(container is IDirectoryItem directory)
                    {
                        directory.IsLoaded = false;
                        directory.IsExpanded = false;
                        _ = directory.ClearChildrenAsync();
                    }
                    renamed = Directory.Exists(existing.FullPath);
                } else if(item is IFileItem file)
                {
                    renamed = File.Exists(file.FullPath);
                }
            }
            return renamed ? FileOperationResult.Ok() : FileOperationResult.Fail("Item not found in the directory");
        }

        public async virtual Task<FileOperationResult> RemoveChildAsync(IEnumerable<ISystemItem> items, Func<ISystemItem, string> keySelector, CancellationToken ct = default)
        {
            if(items is null)
                return FileOperationResult.Fail("Items is null");

            ct.ThrowIfCancellationRequested();
            ISystemItem[] requested = items.ToArray();
            bool removed = false;
            await _dispatcherService.InvokeAsync(() =>
            {
                var references = new HashSet<ISystemItem>(
                    requested,
                    ReferenceEqualityComparer.Instance);
                var keys = new HashSet<string>(
                    requested.Select(keySelector),
                    StringComparer.OrdinalIgnoreCase);
                for(int index = _children.Count - 1; index >= 0; index--)
                {
                    ISystemItem child = _children[index];
                    if(!references.Contains(child) &&
                       !keys.Contains(keySelector(child)))
                    {
                        continue;
                    }

                    _children.RemoveAt(index);
                    if(child is IContainerSystemItem directory)
                        _directoryChildren.Remove(directory);
                    removed = true;
                }
            });
            return removed
                ? FileOperationResult.Ok()
                : FileOperationResult.Fail("Item not found in the directory");
        }

        public async virtual Task<FileOperationResult> ClearChildrenAsync()
        {
            IsLoaded = false;
            await _dispatcherService.InvokeAsync(() =>
            {
                ReplaceChildren(Array.Empty<ISystemItem>());
            });
            if(_children.Count == 0)
                return FileOperationResult.Ok();
            return FileOperationResult.Fail("Failed to clear children");
        }

        public async virtual Task<FileOperationResult> SortingAsync(SystemItemSortType sortType, int dir = 0, CancellationToken ct = default)
        {
            if(_children == null)
                return FileOperationResult.Fail("Items collection is null.");

            ct.ThrowIfCancellationRequested();

            await _dispatcherService.InvokeAsync(() =>
            {
                ISystemItem[] sorted = _children
                    .OrderBy(
                        item => item,
                        _systemItemSortService.GetComparer(sortType, dir))
                    .ToArray();
                ReplaceChildren(sorted);
            });
            return FileOperationResult.Ok();
        }

        public async Task SyncCollectionsAsync(IEnumerable<ISystemItem> source, Func<ISystemItem, string> keySelector,
            Action<ISystemItem, ISystemItem>? updateExisting = null, CancellationToken ct = default)
        {
            // Снимок цели берём на UI-потоке, сравнение выполняем по хеш-индексам.
            var targetSnapshot = await _dispatcherService.InvokeAsync(
                () => _children.ToList());

            // Тяжёлое сравнение — в фоне
            var plan = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                var sourceList = source.ToList();

                // sourceMap: key -> item (последний wins, либо можно выбирать первый)
                var sourceMap = new Dictionary<string, ISystemItem>(StringComparer.OrdinalIgnoreCase);
                foreach(var s in sourceList)
                {
                    var k = keySelector(s);
                    sourceMap[k] = s;
                }

                var targetMap = new Dictionary<string, ISystemItem>(StringComparer.OrdinalIgnoreCase);
                foreach(var t in targetSnapshot)
                {
                    var k = keySelector(t);
                    targetMap[k] = t;
                }

                var toRemoveKeys = targetMap.Keys
                    .Where(k => !sourceMap.ContainsKey(k))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var toAdd = sourceMap.Where(kv => !targetMap.ContainsKey(kv.Key))
                                     .Select(kv => kv.Value)
                                     .ToList();

                var toUpdate = sourceMap.Where(kv => targetMap.ContainsKey(kv.Key))
                                        .Select(kv => (existing: targetMap[kv.Key], incoming: kv.Value))
                                        .ToList();

                return (toRemoveKeys, toAdd, toUpdate);
            }, ct);

            await _dispatcherService.InvokeAsync(new Action(() =>
            {
                var currentKeys = new HashSet<string>(
                    _children.Select(keySelector),
                    StringComparer.OrdinalIgnoreCase);
                var synchronized = new List<ISystemItem>(_children.Count + plan.toAdd.Count);
                foreach(ISystemItem item in _children)
                {
                    ct.ThrowIfCancellationRequested();
                    if(!plan.toRemoveKeys.Contains(keySelector(item)))
                        synchronized.Add(item);
                }

                foreach(var (existing, incoming) in plan.toUpdate)
                    updateExisting?.Invoke(existing, incoming);

                foreach(var item in plan.toAdd)
                {
                    string itemKey = keySelector(item);
                    // FileSystemWatcher мог добавить элемент после построения плана синхронизации.
                    if(currentKeys.Add(itemKey))
                        synchronized.Add(item);
                }

                if(plan.toRemoveKeys.Count > 0 || synchronized.Count != _children.Count)
                    ReplaceChildren(synchronized);
            }));

        }

        private void ReplaceChildren(IReadOnlyCollection<ISystemItem> items)
        {
            _children.ReplaceAll(items);

            IContainerSystemItem[] directories = items
                .OfType<IContainerSystemItem>()
                .ToArray();
            bool directorySetChanged = directories.Length != _directoryChildren.Count ||
                !directories
                    .Zip(_directoryChildren, ReferenceEquals)
                    .All(same => same);

            if(directorySetChanged)
                _directoryChildren.ReplaceAll(directories);
        }

        private void StartMonitoring()
        {
            if(_monitor)
                return;

            _monitor = _directoryMonitoringService.StartMonitoring(FullPath,
                onCreated: e => QueueMonitoringEvent(e.FullPath),
                onDeleted: e => QueueMonitoringEvent(e.FullPath),
                onRenamed: e => QueueMonitoringEvent(e.FullPath, e.OldFullPath),
                onChanged: e => QueueMonitoringEvent(e.FullPath));
        }

        public Task FlushMonitoringAsync(CancellationToken ct = default)
        {
            lock(_monitorEventLock)
                return (_monitorCompletion?.Task ?? Task.CompletedTask).WaitAsync(ct);
        }

        private void QueueMonitoringEvent(string path, string? oldPath = null)
        {
            TaskCompletionSource completion;
            lock(_monitorEventLock)
            {
                _monitorEvents.Add((path, oldPath));
                if(_monitorCompletion is not null)
                    return;

                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _monitorCompletion = completion;
            }
            _ = ProcessMonitoringEventsAsync(completion);
        }

        private async Task ProcessMonitoringEventsAsync(TaskCompletionSource completion)
        {
            try
            {
                while(true)
                {
                    // Одна задержка на пакет, а не на каждый файл. За это время
                    // атомарная запись успевает опубликовать итоговый файл.
                    await Task.Delay(75).ConfigureAwait(false);
                    (string Path, string? OldPath)[] events;
                    lock(_monitorEventLock)
                    {
                        events = _monitorEvents.ToArray();
                        _monitorEvents.Clear();
                    }

                    var incoming = events
                        .SelectMany(e => e.OldPath is null
                            ? new[] { e.Path } : new[] { e.OldPath, e.Path })
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(path => path, CreateItemIfExists,
                            StringComparer.OrdinalIgnoreCase);
                    await _dispatcherService.InvokeAsync(
                        () => ApplyMonitoringEvents(events, incoming)).ConfigureAwait(false);

                    lock(_monitorEventLock)
                    {
                        if(_monitorEvents.Count != 0)
                            continue;
                        _monitorCompletion = null;
                        completion.TrySetResult();
                        return;
                    }
                }
            }
            catch(Exception)
            {
                // В том числе отмена Dispatcher при закрытии приложения.
                // Полная синхронизация каталога остаётся источником истины.
                lock(_monitorEventLock)
                {
                    _monitorEvents.Clear();
                    _monitorCompletion = null;
                    completion.TrySetResult();
                }
            }
        }

        private void ApplyMonitoringEvents(
            (string Path, string? OldPath)[] events,
            Dictionary<string, ISystemItem?> incoming)
        {
            var items = _children.ToDictionary(item => item.FullPath,
                StringComparer.OrdinalIgnoreCase);
            foreach(var change in events)
            {
                // Сохраняем объект и выделение даже для цепочки a -> b -> c.
                if(change.OldPath is not null &&
                   items.Remove(change.OldPath, out ISystemItem? renamed))
                    items.TryAdd(change.Path, renamed);
            }

            foreach(var (path, item) in incoming)
            {
                if(item is null)
                    items.Remove(path);
                else if(items.TryGetValue(path, out ISystemItem? existing) &&
                    (existing is IContainerSystemItem) == (item is IContainerSystemItem))
                    ApplyRenamedItem(existing, item);
                else
                    items[path] = item;
            }

            var remaining = items.Values.ToHashSet(ReferenceEqualityComparer.Instance);
            ISystemItem[] removed = _children.Where(item => !remaining.Contains(item)).ToArray();
            var original = _children.ToHashSet(ReferenceEqualityComparer.Instance);
            ISystemItem[] added = items.Values.Where(item => !original.Contains(item)).ToArray();
            if(removed.Length + added.Length > 1)
            {
                ReplaceChildren(_children.Where(remaining.Contains).Concat(added).ToArray());
            }
            else if(removed.Length == 1)
            {
                _children.Remove(removed[0]);
                if(removed[0] is IContainerSystemItem directory)
                    _directoryChildren.Remove(directory);
            }
            else if(added.Length == 1)
            {
                _children.Add(added[0]);
                if(added[0] is IContainerSystemItem directory)
                    _directoryChildren.Add(directory);
            }
        }

        private void StopMonitoring()
        {
            if(!(IsExpanded || IsSelected))
                _monitor = !_directoryMonitoringService.StopMonitoring(FullPath);
        }

        private ISystemItem? CreateItemIfExists(string fullPath)
        {
            if(string.IsNullOrWhiteSpace(fullPath))
                return null;

            var path = Path.GetFullPath(fullPath);
            try
            {
                if(Directory.Exists(path))
                    return _systemItemCreateService.CreateDirectory(path, this);
                if(File.Exists(path))
                    return _systemItemCreateService.CreateFile(path, this);
                return null;
            }
            catch(Exception exception) when(
                exception is IOException or UnauthorizedAccessException)
            {
                // Между Exists и чтением метаданных внешний процесс мог снова
                // переместить или удалить элемент.
                return null;
            }
        }

        private void ApplyRenamedItem(
            ISystemItem existing,
            ISystemItem incoming)
        {
            ContainerSystemItem? monitoredContainer =
                existing as ContainerSystemItem;
            bool restartMonitoring = monitoredContainer?._monitor == true &&
                !string.Equals(existing.FullPath, incoming.FullPath, StringComparison.OrdinalIgnoreCase);
            if(restartMonitoring)
            {
                monitoredContainer!._directoryMonitoringService.StopMonitoring(
                    existing.FullPath);
                monitoredContainer._monitor = false;
            }

            existing.Name = incoming.Name;
            existing.FullPath = incoming.FullPath;
            existing.DisplayPath = incoming.DisplayPath;
            existing.RootDirectory = incoming.RootDirectory;
            existing.Size = incoming.Size;
            existing.LastWriteTimeUtc = incoming.LastWriteTimeUtc;
            existing.Parent = this;

            if(existing is IFileItem existingFile &&
               incoming is IFileItem incomingFile)
            {
                existingFile.Extension = incomingFile.Extension;
                existingFile.IsHidden = incomingFile.IsHidden;
                existingFile.IsReadOnly = incomingFile.IsReadOnly;
            }

            if(restartMonitoring)
                monitoredContainer!.StartMonitoring();
        }

    }
}

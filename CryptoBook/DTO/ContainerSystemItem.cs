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
        private readonly SemaphoreSlim _monitorEventGate = new(1, 1);

        public string Name { get => name; set => SetProperty(ref name, value); }
        string name;
        public string RootDirectory { get => _rootDirectory; set => SetProperty(ref _rootDirectory, value); }
        string _rootDirectory;
        /// <summary>
        /// полный путь к директории
        /// </summary>
        public string FullPath { get => fullPath; set => SetProperty(ref fullPath, value); }
        string fullPath;
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
                onCreated: e => _ = HandleCreatedAsync(e.FullPath),
                onDeleted: e => _ = HandleDeletedAsync(e.FullPath),
                onRenamed: e => _ = HandleRenamedAsync(e.OldFullPath, e.FullPath),
                 onChanged: async (e) =>
                 {
                     await _dispatcherService.InvokeAsync(new Action(() =>
                     {
                         if(e.ChangeType != WatcherChangeTypes.Created && e.ChangeType != WatcherChangeTypes.Renamed)
                         {
                             var item = _children.Where<ISystemItem>(x => string.Equals(x.FullPath, e.FullPath, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                             var info = new FileInfo(e.FullPath);
                             if(item is not null && info.Exists)
                             {
                                 item.Size = info.Length;
                                 item.LastWriteTimeUtc = info.LastWriteTime.ToLocalTime();
                             }
                         }
                     }));
                 });
        }

        private async Task HandleCreatedAsync(string fullPath)
        {
            // Сериализация событий не позволяет временным файлам обогнать их удаление.
            await _monitorEventGate.WaitAsync();
            try
            {
                // Атомарная запись создаёт краткоживущий *.tmp. Даём операции
                // завершить Move/Delete и добавляем только существующий результат.
                await Task.Delay(75);
                ISystemItem? item = CreateItemIfExists(fullPath);
                if(item is not null)
                    await AddChildAsync([item], child => child.FullPath);
            }
            finally
            {
                _monitorEventGate.Release();
            }
        }

        private async Task HandleDeletedAsync(string fullPath)
        {
            await _monitorEventGate.WaitAsync();
            try
            {
                ISystemItem? existing = await _dispatcherService.InvokeAsync(
                    () => _children.FirstOrDefault(item =>
                        string.Equals(
                            item.FullPath,
                            fullPath,
                            StringComparison.OrdinalIgnoreCase)));
                if(existing is not null)
                    await RemoveChildAsync([existing], item => item.FullPath);
            }
            finally
            {
                _monitorEventGate.Release();
            }
        }

        private async Task HandleRenamedAsync(string oldFullPath, string newFullPath)
        {
            await _monitorEventGate.WaitAsync();
            try
            {
                await Task.Delay(75);
                ISystemItem? incoming = CreateItemIfExists(newFullPath);
                ISystemItem? existing = await _dispatcherService.InvokeAsync(
                    () => _children.FirstOrDefault(item =>
                        string.Equals(
                            item.FullPath,
                            oldFullPath,
                            StringComparison.OrdinalIgnoreCase)));

                if(existing is null)
                {
                    if(incoming is not null)
                        await AddChildAsync([incoming], item => item.FullPath);
                    return;
                }

                if(incoming is null)
                {
                    await RemoveChildAsync([existing], item => item.FullPath);
                    return;
                }

                // Rename обновляет существующий объект на месте. ListView сохраняет
                // выделение, preview и позицию прокрутки.
                await _dispatcherService.InvokeAsync(
                    () => ApplyRenamedItem(existing, incoming));
            }
            finally
            {
                _monitorEventGate.Release();
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
            bool restartMonitoring = monitoredContainer?._monitor == true;
            if(restartMonitoring)
            {
                monitoredContainer!._directoryMonitoringService.StopMonitoring(
                    existing.FullPath);
                monitoredContainer._monitor = false;
            }

            existing.Name = incoming.Name;
            existing.FullPath = incoming.FullPath;
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

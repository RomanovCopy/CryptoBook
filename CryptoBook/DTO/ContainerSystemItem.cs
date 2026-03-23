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

        public long? Size { get => _size; set => SetProperty(ref _size, _children.Sum(x => x.Size)); }
        long? _size;


        /// <summary>
        /// Возвращает доступную только для чтения наблюдаемую коллекцию дочерних элементов,
        /// содержащихся в этом элементе файловой системы.
        /// </summary>
        /// <remarks>Коллекция отражает текущий набор дочерних элементов и уведомляет наблюдателей об изменениях,
        /// таких как добавление или удаление. Коллекция пуста, если у элемента нет дочерних элементов.</remarks>
        public ReadOnlyObservableCollection<ISystemItem> Children { get; private set; }
        protected ObservableCollection<ISystemItem> _children;

        public ReadOnlyObservableCollection<IContainerSystemItem> FilteredChildren { get; private set; }

        public DateTime LastWriteTimeUtc { get => lastWriteTimeUtc; set => SetProperty(ref lastWriteTimeUtc, value); }
        DateTime lastWriteTimeUtc;

        protected ContainerSystemItem(IDispatcherService dispatcherService, IDirectoryMonitoringService directoryMonitoringService, ISystemItemCreateService systemItemCreateService, ISystemItemSortService systemItemSortService)
        {
            _dispatcherService = dispatcherService;
            _directoryMonitoringService = directoryMonitoringService;
            _systemItemCreateService = systemItemCreateService;
            _systemItemSortService = systemItemSortService;
            _children = new ObservableCollection<ISystemItem>();
            Children = new ReadOnlyObservableCollection<ISystemItem>(_children);
            FilteredChildren = new FilteredReadOnlyObservableCollection<ISystemItem, IContainerSystemItem>(_children).View;
        }

        public async virtual Task<FileOperationResult> AddChildAsync(IEnumerable<ISystemItem> items,
        Func<ISystemItem, string> keySelector, CancellationToken ct = default)
        {
            if(items is null)
                return FileOperationResult.Fail("Items is null");

            if(keySelector is null)
                return FileOperationResult.Fail("Key selector is null");

            ct.ThrowIfCancellationRequested();

            bool exists = false;
            // Решение принимаем снаружи UI-потока, но саму мутацию — только на UI
            await _dispatcherService.InvokeAsync(() =>
            {
                foreach(var item in items)
                {
                    var key = keySelector(item);
                    exists = _children.Any(c => keySelector(c).Equals(key, StringComparison.OrdinalIgnoreCase));
                    if(!exists)
                        _children.Add(item);
                }
            });
            return exists ? FileOperationResult.Fail("Item already exists") : FileOperationResult.Ok();
        }

        public async virtual Task<FileOperationResult> RemoveChildAsync(IEnumerable<ISystemItem> items, Func<ISystemItem, string> keySelector, CancellationToken ct = default)
        {
            if(items is null)
                return FileOperationResult.Fail("Items is null");

            ct.ThrowIfCancellationRequested();
            bool removed = false;
            ISystemItem? existing = null;
            await _dispatcherService.InvokeAsync(() =>
            {
                foreach(var item in items)
                {
                    // передали тот же объект, что хранится в _children
                    existing = _children.FirstOrDefault(c => ReferenceEquals(c, items));
                    //Если объект другой инстанс, но описывает тот же элемент — пробуем по имени
                    // (в пределах одной директории имя уникально в Windows)
                    existing ??= _children.FirstOrDefault(c =>
                        string.Equals(c.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase));
                    removed = existing is not null && _children.Remove(item);
                }
            });
            return existing is null ? FileOperationResult.Fail("Item not found in the directory") : removed ? FileOperationResult.Ok() : FileOperationResult.Fail("Failed to remove item from the directory");
        }

        public async virtual Task<FileOperationResult> ClearChildrenAsync()
        {
            IsLoaded = false;
            await _dispatcherService.InvokeAsync(() =>
            {
                _children.Clear();
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
                _systemItemSortService.Sort(_children, sortType, dir);
            });
            return FileOperationResult.Ok();
        }

        public async Task SyncCollectionsAsync(IEnumerable<ISystemItem> source, Func<ISystemItem, string> keySelector,
            Action<ISystemItem, ISystemItem>? updateExisting = null, CancellationToken ct = default)
        {
            // Снимок цели на UI потоке (если _children привязан к UI)
            var targetSnapshot = _children.ToList();

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

                var toRemoveKeys = targetMap.Keys.Where(k => !sourceMap.ContainsKey(k)).ToList();
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
                for(int i = _children.Count - 1; i >= 0; i--)
                {
                    ct.ThrowIfCancellationRequested();
                    var key = keySelector(_children[i]);
                    if(plan.toRemoveKeys.Contains(key))
                        _children.RemoveAt(i);
                }

                foreach(var (existing, incoming) in plan.toUpdate)
                    updateExisting?.Invoke(existing, incoming);

                foreach(var item in plan.toAdd)
                    _children.Add(item);
            }));

        }

        private void StartMonitoring()
        {
            if(_monitor)
                return;

            _monitor = _directoryMonitoringService.StartMonitoring(FullPath,
                onCreated: async (e) => await AddChildAsync(SystemItemCreate("Created", e.FullPath), x => x.FullPath),
                onDeleted: async (e) => await RemoveChildAsync(SystemItemCreate("Deleted", e.FullPath), x => x.FullPath),
                onRenamed: async (e) =>
                {
                    await RemoveChildAsync(SystemItemCreate("Deleted", e.OldFullPath), x => x.FullPath);
                    await AddChildAsync(SystemItemCreate("Created", e.FullPath), x => x.FullPath);
                },
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

        private void StopMonitoring()
        {
            if(!(IsExpanded || IsSelected))
                _monitor = !_directoryMonitoringService.StopMonitoring(FullPath);
        }

        private List<ISystemItem> SystemItemCreate(string changeType, string fullPath)
        {
            var items = new List<ISystemItem>();
            if(string.IsNullOrWhiteSpace(fullPath))
                return items;
            var path = Path.GetFullPath(fullPath);

            switch(changeType)
            {
                case "Deleted":
                {
                    var existing = Children.FirstOrDefault(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase));
                    if(existing != null)
                        items.Add(existing);
                    break;
                }
                case "Created":
                {
                    if(Directory.Exists(path) && !Children.Any(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        var dirItem = _systemItemCreateService.CreateDirectory(path, this);
                        items.Add(dirItem);
                    } else if(File.Exists(path) && !Children.Any(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        var fileItem = _systemItemCreateService.CreateFile(path, this);
                        items.Add(fileItem);
                    }
                    break;
                }
            }
            return items;
        }

    }
}

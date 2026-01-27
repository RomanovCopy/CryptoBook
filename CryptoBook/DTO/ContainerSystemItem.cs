using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public abstract class ContainerSystemItem: ViewModelBase, IContainerSystemItem
    {
        private readonly IDispatcherService _dispatcherService;

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

        public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
        bool _isExpanded;

        public long Size { get => _size; set => SetProperty(ref _size, _children.Sum(x => x.Size)); }
        long _size;


        /// <summary>
        /// Возвращает доступную только для чтения наблюдаемую коллекцию дочерних элементов,
        /// содержащихся в этом элементе файловой системы.
        /// </summary>
        /// <remarks>Коллекция отражает текущий набор дочерних элементов и уведомляет наблюдателей об изменениях,
        /// таких как добавление или удаление. Коллекция пуста, если у элемента нет дочерних элементов.</remarks>
        public ReadOnlyObservableCollection<ISystemItem> Children { get; private set; }
        protected ObservableCollection<ISystemItem> _children;


        public DateTime LastWriteTimeUtc { get => lastWriteTimeUTc; set => SetProperty(ref lastWriteTimeUTc, value); }
        DateTime lastWriteTimeUTc;

        protected ContainerSystemItem(IDispatcherService dispatcherService)
        {
            _children = new ObservableCollection<ISystemItem>();
            Children = new ReadOnlyObservableCollection<ISystemItem>(_children);
            _dispatcherService = dispatcherService;
        }

        public virtual Task<FileOperationResult> AddChildAsync( ISystemItem item, Func<ISystemItem, string> keySelector, CancellationToken ct = default)
        {
            if(item is null)
                return Task.FromResult(FileOperationResult.Fail("Item is null"));

            if(keySelector is null)
                return Task.FromResult(FileOperationResult.Fail("Key selector is null"));

            if(item is not (IFileItem or IDirectoryItem))
                return Task.FromResult(FileOperationResult.Fail("Item must be of type IFileItem or IDirectoryItem"));

            if(ct.IsCancellationRequested)
                return Task.FromCanceled<FileOperationResult>(ct);

            // Решение принимаем снаружи UI-потока, но саму мутацию — только на UI
            return _dispatcherService.InvokeAsync(() =>
            {
                ct.ThrowIfCancellationRequested();

                var key = keySelector(item);

                // Важно: проверка дубликатов должна быть на UI, иначе гонки
                if(_children.Any(c => keySelector(c).Equals(key, StringComparison.OrdinalIgnoreCase)))
                    return FileOperationResult.Fail("Item already exists");

                _children.Add(item);
                return FileOperationResult.Ok();
            });
        }



        public virtual FileOperationResult RemoveChild(ISystemItem item)
        {
            if(item is null)
                return FileOperationResult.Fail("Item is null");

            if(item is not (IFileItem or IDirectoryItem))
                return FileOperationResult.Fail("Item must be of type IFileItem or IDirectoryItem");

            // передали тот же объект, что хранится в _children
            var existing = _children.FirstOrDefault(c => ReferenceEquals(c, item));

            //Если объект другой инстанс, но описывает тот же элемент — пробуем по имени
            // (в пределах одной директории имя уникально в Windows)
            existing ??= _children.FirstOrDefault(c =>
                string.Equals(c.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase));

            if(existing is null)
                return FileOperationResult.Fail("Item not found in the directory");

            var removed = _children.Remove(existing);
            return removed
                ? FileOperationResult.Ok()
                : FileOperationResult.Fail("Failed to remove item from the directory");
        }



        public virtual FileOperationResult ClearChildren()
        {
            _children.Clear();
            IsLoaded = false;
            IsExpanded = false;
            if(_children.Count == 0)
                return FileOperationResult.Ok();
            return FileOperationResult.Fail("Failed to clear children");
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
    }
}

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

        protected ContainerSystemItem()
        {
            _children = new ObservableCollection<ISystemItem>();
            Children = new ReadOnlyObservableCollection<ISystemItem>(_children);
        }

        public virtual FileOperationResult AddChild(ISystemItem item)
        {
            if(item is null)
                return FileOperationResult.Fail("Item is null");


            if(item is not (IFileItem or IDirectoryItem))
                return FileOperationResult.Fail("Item must be of type IFileItem or IDirectoryItem");

            //никаих проверок не делаем т.к. это лишь слепок уже существующей директории

            _children.Add(item);
            return FileOperationResult.Ok();
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

        public virtual async void SyncCollectionsAsync(IEnumerable<ISystemItem> source, Func<ISystemItem, string> keySelector)
        {
            await Task.Run(() =>
            {
                var sourceList = source.ToList();
                var sourceKeys = sourceList.ToDictionary(keySelector);
                var targetKeys = _children.ToList().ToDictionary(keySelector);
                // 1. Удаляем те, которых больше нет в источнике
                for(int i = _children.Count - 1; i >= 0; i--)
                {
                    if(!sourceKeys.ContainsKey(keySelector(_children[i])))
                        _children.RemoveAt(i);
                }

                // 2. Обновляем существующие и добавляем новые
                foreach(var sourceItem in sourceList)
                {
                    var key = keySelector(sourceItem);
                    if(targetKeys.TryGetValue(key, out var existingItem))
                    {
                        // Логика обновления свойств существующего объекта
                    } else
                    {
                        _children.Add(sourceItem);
                    }
                }
            });
        }

    }
}

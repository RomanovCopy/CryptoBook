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
    public class DirectoryItem:ViewModelBase,IDirectoryItem
    {
        /// <summary>
        /// имя директории
        /// </summary>
        public string Name { get => name; set => SetProperty(ref name, value); }
        string name;
        /// <summary>
        /// полный путь к директории
        /// </summary>
        public string FullPath { get => fullPath; set => SetProperty(ref fullPath, value); }
        string fullPath;
        /// <summary>
        /// родительская директория(если лежит на диске то null)
        /// </summary>
        public IDirectoryItem? Parent { get=> parent; set => SetProperty(ref parent, value); }
        IDirectoryItem? parent;
        /// <summary>
        /// родительский корневой элемент(диск)
        /// </summary>
        public IRootItem Root { get => root; set => SetProperty(ref root, value); }
        IRootItem root;
        /// <summary>
        /// флаг - дочерние элементы загружены
        /// </summary>
        public bool IsLoaded { get => isLoaded; set => SetProperty(ref isLoaded, value); }
        bool isLoaded;


        /// <summary>
        /// Возвращает доступную только для чтения наблюдаемую коллекцию дочерних элементов,
        /// содержащихся в этом элементе файловой системы.
        /// </summary>
        /// <remarks>Коллекция отражает текущий набор дочерних элементов и уведомляет наблюдателей об изменениях,
        /// таких как добавление или удаление. Коллекция пуста, если у элемента нет дочерних элементов.</remarks>
        public ReadOnlyObservableCollection<IFileSystemItem> Children { get; private set; }
        ObservableCollection<IFileSystemItem> _children;

        public bool IsHidden { get => isHidden; set => SetProperty(ref isHidden, value); }
        bool isHidden;

        public bool IsReadOnly { get => isReadOnly; set => SetProperty(ref isReadOnly, value); }
        bool isReadOnly;

        public bool IsDirectory { get => isDirectory; set => SetProperty(ref isDirectory, value); }
        bool isDirectory;

        public DateTime LastWriteTimeUtc { get => lastWriteTimeUTc; set => SetProperty(ref lastWriteTimeUTc, value); }
        DateTime lastWriteTimeUTc;

        public DirectoryItem()
        {
            _children = new ObservableCollection<IFileSystemItem>();
            Children = new ReadOnlyObservableCollection<IFileSystemItem>(_children);
        }

    }
}

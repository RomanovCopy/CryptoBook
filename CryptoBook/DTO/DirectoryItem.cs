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
        private readonly IFileManagerService _fileManagerService;


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
        public readonly ObservableCollection<FileItem> Children;

        public bool IsHidden { get => isHidden; set => SetProperty(ref isHidden, value); }
        bool isHidden;

        public bool IsReadOnly { get => isReadOnly; set => SetProperty(ref isReadOnly, value); }
        bool isReadOnly;

        public bool IsDirectory { get => isDirectory; set => SetProperty(ref isDirectory, value); }
        bool isDirectory;

        public DirectoryItem( IFileManagerService? fileManagerService)
        {
            _fileManagerService = fileManagerService??throw new ArgumentNullException(nameof(fileManagerService));
            Children = new ObservableCollection<FileItem>();
        }




        /// <summary>
        /// Обеспечивает асинхронную загрузку необходимых данных, если они еще не загружены.
        /// </summary>
        /// <param name="ct">Маркер отмены, который можно использовать для отмены асинхронной операции. </param>
        /// <returns>Задача, представляющая асинхронную операцию загрузки. Задача завершается после загрузки данных. </returns>
        public async Task EnsureLoadedAsync(CancellationToken ct = default)
        {
            var list = await _fileManagerService.BrowseAsync(FullPath, ct, true);
            foreach(var item in list)
            {
                Children.Add(item);
            }
        }

        /// <summary>
        /// Инвалидирует текущие данные, помечая их как устаревшие и требующие перезагрузки при следующем доступе.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Invalidate()
        {
            throw new NotImplementedException();
        }



        public Task RefreshAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task AddChildAsync(IFileSystemItem item, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveChildAsync(IFileSystemItem item, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task ClearChildrenAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<IFileItem> CreateFileAsync(string name, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<IDirectoryItem> CreateDirectoryAsync(string name, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

    }
}

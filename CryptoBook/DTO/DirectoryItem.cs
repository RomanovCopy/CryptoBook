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

        public ReadOnlyObservableCollection<IFileSystemItem> Children => throw new NotImplementedException();

        public bool IsLoaded => throw new NotImplementedException();

        public Task EnsureLoadedAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public void Invalidate()
        {
            throw new NotImplementedException();
        }
    }
}

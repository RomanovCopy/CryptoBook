using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    internal class FileSystemItem:ViewModelBase, IFileSystemItem
    {
        public string Name { get => name; set => SetProperty(ref name, value); }
        string name;

        public string FullPath { get => fullPath; set => SetProperty(ref fullPath, value); }
        string fullPath;

        public bool IsDirectory { get => isDirectory; set => SetProperty(ref isDirectory, value); }
        bool isDirectory;

        public bool IsHidden { get => isHidden; set => SetProperty(ref isHidden, value); }
        bool isHidden;

        public bool IsReadOnly { get => isReadOnly; set => SetProperty(ref isReadOnly, value); }
        bool isReadOnly;

        public IDirectoryItem? Parent { get => parent; set => SetProperty(ref parent, value); }
        IDirectoryItem? parent;

        public IRootItem Root { get => root; set => SetProperty(ref root, value); }
        IRootItem root;

        public DateTime LastWriteTimeUtc { get => lastWriteTimeUtc; set => SetProperty(ref lastWriteTimeUtc, value); }
        DateTime lastWriteTimeUtc;
    }
}

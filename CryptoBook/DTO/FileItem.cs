using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class FileItem: ViewModelBase, IFileItem
    {
        public string FullPath { get => fullPath; set => SetProperty(ref fullPath, value); }
        string fullPath;
        public string RootDirectory { get => _rootDirectory; set => SetProperty(ref _rootDirectory, value); }
        string _rootDirectory;

        public string Name { get => name; set => SetProperty(ref name, value); }
        string name;
        public long? Size { get => size; set => SetProperty(ref size, value); }
        long? size;
        public string Extension { get => extension??"Folder"; set => SetProperty(ref extension, value); }
        string extension;
        public bool IsHidden { get => isHidden; set => SetProperty(ref isHidden, value); }
        bool isHidden;
        public bool IsReadOnly { get => isReadOnly; set => SetProperty(ref isReadOnly, value); }
        bool isReadOnly;
        public bool IsEditing { get => isEditing; set => SetProperty(ref isEditing, value); }
        bool isEditing;
        public DateTime LastWriteTimeUtc { get => lastWriteTimeUtc; set => SetProperty(ref lastWriteTimeUtc, value); }
        DateTime lastWriteTimeUtc;
        public ISystemItem? Parent { get => parent; set => SetProperty(ref parent, value); }
        ISystemItem? parent;
    }
}

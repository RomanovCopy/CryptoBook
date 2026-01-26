using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    internal class SystemItem:ViewModelBase, ISystemItem
    {
        public string FullPath { get => fullPath; set => SetProperty(ref fullPath, value); }
        string fullPath;
        public string RootDirectory { get => _rootDirectory; set => SetProperty(ref _rootDirectory, value); }
        string _rootDirectory;

        public DateTime LastWriteTimeUtc { get => lastWriteTimeUtc; set => SetProperty(ref lastWriteTimeUtc, value); }
        DateTime lastWriteTimeUtc;

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        string _name;

        public long Size { get => _size; set => SetProperty(ref _size, value); }
        long _size;

        public ISystemItem? Parent { get => _parent; set => SetProperty(ref _parent, value); }
        ISystemItem? _parent;

        public SystemItem()
        {
        }
    }
}

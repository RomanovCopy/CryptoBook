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
        public string Name { get => name; set => SetProperty(ref name, value); }
        string name;
        public long Size { get => size; private set => SetProperty(ref size, value); }
        long size;
        public string Extension { get => extension; private set => SetProperty(ref extension, value); }
        string extension;
        public bool IsHidden { get => isHidden; private set => SetProperty(ref isHidden, value); }
        bool isHidden;
        public bool IsReadOnly { get => isReadOnly; private set => SetProperty(ref isReadOnly, value); }
        bool isReadOnly;
        public bool IsDirectory { get => isDirectory; private set => SetProperty(ref isDirectory, value); }
        bool isDirectory;

        public DateTime LastWriteTimeUtc { get => lastWriteTimeUtc; private set => SetProperty(ref lastWriteTimeUtc, value); }
        DateTime lastWriteTimeUtc;
        public IDirectoryItem? Parent { get => parent; private set => SetProperty(ref parent, value); }
        IDirectoryItem? parent;
        public IRootItem Root { get => root; private set => SetProperty(ref root, value); }
        IRootItem root;


        public FileItem(string fullPath)
        {
            FullPath = fullPath;
        }

        private void GetFileProperties()
        {
            bool isDir = info is DirectoryInfo;

            long? size = null;
            if(!isDir && info is FileInfo fi)
            {
                size = fi.Length;
            }

            bool isHidden = (info.Attributes & FileAttributes.Hidden) != 0;
            bool isReadOnly = (info.Attributes & FileAttributes.ReadOnly) != 0;

                Name = info.Name,
                IsDirectory = isDir,
                Size = size,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                IsHidden = isHidden,
                IsReadOnly = isReadOnly
        }
    }
}

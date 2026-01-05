using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class FileItem:ViewModelBase
    {
        public string FullPath { get=>fullPath; set=>SetProperty(ref fullPath,value); }
        string fullPath;
        public string Name { get=>name; set=>SetProperty(ref name,value); }
        string name;
        public bool IsDirectory { get => isDirectory; set =>SetProperty(ref isDirectory,value); }
        bool isDirectory;
        public long? SizeBytes { get => sizeBytes; set =>SetProperty(ref sizeBytes,value); }
        long? sizeBytes;
        public DateTime LastWriteTimeUtc { get => lastWriteTimeUtc; set => SetProperty(ref lastWriteTimeUtc,value); }
        DateTime lastWriteTimeUtc;
        public bool IsHidden { get => isHidden; set =>SetProperty(ref isHidden,value); }
        bool isHidden;
        public bool IsReadOnly { get => isReadOnly; set =>SetProperty(ref isReadOnly,value); }
        bool isReadOnly;
    }
}

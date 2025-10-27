using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class FileItem:IFileItem
    {
        public string FullPath { get=>fullPath; init=>fullPath=value; }
        string fullPath;
        public string Name { get=>name; init=>name=value; }
        string name;
        public bool IsDirectory { get => isDirectory; init => isDirectory=value; }
        bool isDirectory;
        public long? SizeBytes { get => sizeBytes; init => sizeBytes=value; }
        long? sizeBytes;
        public DateTime LastWriteTimeUtc { get => lastWriteTimeUtc; init => lastWriteTimeUtc=value; }
        DateTime lastWriteTimeUtc;
        public bool IsHidden { get => isHidden; init =>isHidden=value; }
        bool isHidden;
        public bool IsReadOnly { get => isReadOnly; init =>isReadOnly=value; }
        bool isReadOnly;
    }
}

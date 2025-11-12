using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class DirectoryContent
    {
        public string DirectoryPath { get => directoryPath; init => directoryPath=value; }
        string directoryPath;
        public IReadOnlyList<FileItem> Items { get => items; init => items=value; }
        IReadOnlyList<FileItem> items;
    }
}

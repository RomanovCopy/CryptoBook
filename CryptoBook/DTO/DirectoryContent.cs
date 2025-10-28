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
        public IReadOnlyList<IFileItem> Items { get => throw new NotImplementedException(); init => throw new NotImplementedException(); }
    }
}

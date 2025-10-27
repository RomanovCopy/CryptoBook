using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDirectoryContent
    {
        public string DirectoryPath { get; init; }      // "C:\Temp"
        public IReadOnlyList<IFileItem> Items { get; init; }
    }
}

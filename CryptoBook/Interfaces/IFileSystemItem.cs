using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileSystemItem
    {
        string Name { get; }
        string FullPath { get; }

        IDirectoryItem? Parent { get; }   // у корня диска Parent = null
        IRootItem Root { get; }           // всегда НЕ null
    }
}

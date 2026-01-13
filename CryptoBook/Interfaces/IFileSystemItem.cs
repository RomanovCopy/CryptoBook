using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// содержит имя и путь к файлу, а так же его родителей(диск и директория)
    /// </summary>
    public interface IFileSystemItem
    {
        string FullPath { get; }

        bool IsHidden { get; }
        bool IsReadOnly { get; }

        IDirectoryItem? Parent { get; }   // у корня диска Parent = null
        IRootItem Root { get; }           // всегда НЕ null
    }
}

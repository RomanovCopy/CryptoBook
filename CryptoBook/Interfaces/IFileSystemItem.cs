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
        string Name { get; set; }
        string FullPath { get; set; }
        bool IsDirectory { get; set; }
        bool IsHidden { get; set; }
        bool IsReadOnly { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        IDirectoryItem? Parent { get; set; }   // у корня диска Parent = null
        IRootItem Root { get; set; }           // всегда НЕ null
    }
}

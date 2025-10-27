using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileItem
    {
        public string FullPath { get; init; }           // "C:\Temp\readme.txt"
        public string Name { get; init; }               // "readme.txt"
        public bool IsDirectory { get; init; }

        public long? SizeBytes { get; init; }                    // null для директорий
        public DateTime LastWriteTimeUtc { get; init; }

        // Флаги
        public bool IsHidden { get; init; }
        public bool IsReadOnly { get; init; }
    }
}

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
    public interface ISystemItem
    {
        string RootDirectory { get; set; }  // Например, "E:\"
        string FullPath { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
    }
}

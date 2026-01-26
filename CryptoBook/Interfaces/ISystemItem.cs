using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// содержит имя и путь к файлу, а так же его родителей(диск и директория)
    /// </summary>
    public interface ISystemItem
    {
        string Name { get; set; }
        string FullPath { get; set; }
        string RootDirectory { get; set; }
        long Size { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        [JsonIgnore]
        ISystemItem? Parent { get; set; }
    }
}

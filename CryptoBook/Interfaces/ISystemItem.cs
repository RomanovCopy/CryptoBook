using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// содержит имя и путь к файлу, а так же его родителей(диск и директория)
    /// </summary>
    public interface ISystemItem:INotifyPropertyChanged
    {
        string Name { get; set; }
        string FullPath{  get; set; }
        string DisplayPath
        {
            get => FullPath;
            set { }
        }
        StorageLocation Location
        {
            get => StorageLocation.Parse(FullPath);
            set => FullPath = value.ToString();
        }
        StorageProviderCapabilities Capabilities
        {
            get => StorageProviderCapabilities.None;
            set { }
        }
        string? StatusText
        {
            get => null;
            set { }
        }
        string RootDirectory { get; set; }
        long Size { get; set; }
        bool IsEditing { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        [JsonIgnore]
        ISystemItem? Parent { get; set; }
    }
}

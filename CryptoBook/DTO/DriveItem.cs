using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class DriveItem: ViewModelBase,IDriveItem
    {
        public string RootDirectory { get=>rootDirectory; set=>SetProperty(ref rootDirectory, value); }  // Например, "E:\"
        string rootDirectory;
        public string VolumeLabel { get=>volumeLabel; set=>SetProperty(ref volumeLabel, value); }
        string volumeLabel;
        public string DriveFormat { get=>driveFormat; set=>SetProperty(ref driveFormat, value); }
        string driveFormat;
        public DriveType DriveType { get=>driveType; set=>SetProperty(ref driveType, value); }
        DriveType driveType;
        public long AvailableFreeSpace { get=>availableFreeSpace; set=>SetProperty(ref availableFreeSpace, value); }  // В байтах
        long availableFreeSpace;
        public long TotalSize { get=>totalSize; set=>SetProperty(ref totalSize, value); } // В байтах
        long totalSize;

        public ReadOnlyObservableCollection<IFileSystemItem> Children{ get; private set; }
        ObservableCollection<IFileSystemItem> _children;

        public bool IsLoaded{ get => _isLoaded; set => SetProperty(ref _isLoaded, value); }
        bool _isLoaded;

        public DriveItem()
        {
            _children = new ObservableCollection<IFileSystemItem>();
            Children = new ReadOnlyObservableCollection<IFileSystemItem>(_children);
        }





        public override string ToString()
        {
            return $"{RootDirectory} ({VolumeLabel}) - {DriveType}, {DriveFormat}, Свободно: {AvailableFreeSpace / (1024 * 1024 * 1024)} ГБ";
        }

    }
}

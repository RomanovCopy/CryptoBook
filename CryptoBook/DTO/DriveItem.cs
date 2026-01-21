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
        public string RootDirectory { get => rootDirectory; set => SetProperty(ref rootDirectory, value); }  // Например, "E:\"
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

        public ReadOnlyObservableCollection<ISystemItem> Children{ get; private set; }
        ObservableCollection<ISystemItem> _children;

        public bool IsLoaded{ get => _isLoaded; set => SetProperty(ref _isLoaded, value); }
        bool _isLoaded;

        public bool IsExpanded { get =>_isExpanded; set =>SetProperty(ref _isExpanded, value); }
        bool _isExpanded;
        public string FullPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime LastWriteTimeUtc { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public DriveItem()
        {
            _children = new ObservableCollection<ISystemItem>();
            Children = new ReadOnlyObservableCollection<ISystemItem>(_children);
        }


        public FileOperationResult AddChild(ISystemItem item)
        {
            if(item is null)
                return FileOperationResult.Fail("Item is null");


            if(item is not (IFileItem or IDirectoryItem))
                return FileOperationResult.Fail("Item must be of type IFileItem or IDirectoryItem");

            //никаих проверок не делаем т.к. это лишь слепок уже существующей директории

            _children.Add(item);
            return FileOperationResult.Ok();
        }

        public FileOperationResult RemoveChild(ISystemItem item)
        {
            if(item is null)
                return FileOperationResult.Fail("Item is null");

            if(item is not (IFileItem or IDirectoryItem))
                return FileOperationResult.Fail("Item must be of type IFileItem or IDirectoryItem");

            // передали тот же объект, что хранится в _children
            var existing = _children.FirstOrDefault(c => ReferenceEquals(c, item));

            //Если объект другой инстанс, но описывает тот же элемент — пробуем по имени
            // (в пределах одной директории имя уникально в Windows)
            existing ??= _children.FirstOrDefault(c =>
                string.Equals(c.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase));

            if(existing is null)
                return FileOperationResult.Fail("Item not found in the directory");

            var removed = _children.Remove(existing);
            return removed
                ? FileOperationResult.Ok()
                : FileOperationResult.Fail("Failed to remove item from the directory");
        }



        public override string ToString()
        {
            return $"{RootDirectory} ({VolumeLabel}) - {DriveType}, {DriveFormat}, Свободно: {AvailableFreeSpace / (1024 * 1024 * 1024)} ГБ";
        }

    }
}

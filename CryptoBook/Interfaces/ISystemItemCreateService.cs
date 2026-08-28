using System;
using CryptoBook.DTO;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ISystemItemCreateService:IService
    {
        IDriveItem CreateRoot(string rootPath);
        IDirectoryItem CreateDirectory(string path, ISystemItem? parent);
        IFileItem CreateFile(string path, ISystemItem? parent);

        IDriveItem CreateRoot(StorageItemMetadata metadata) =>
            throw new NotSupportedException();
        IDirectoryItem CreateDirectory(
            StorageItemMetadata metadata,
            ISystemItem parent) => throw new NotSupportedException();
        IFileItem CreateFile(
            StorageItemMetadata metadata,
            ISystemItem parent) => throw new NotSupportedException();
    }
}


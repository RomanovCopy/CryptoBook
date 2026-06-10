using System;
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
    }
}


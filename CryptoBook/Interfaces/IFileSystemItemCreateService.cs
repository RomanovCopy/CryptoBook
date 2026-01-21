using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ISystemItemCreateService:IService
    {
        IRootItem CreateRoot(string rootPath);
        IDirectoryItem CreateDirectory(string path, IContainerSystemItem parent);
        IFileItem CreateFile(string path, IContainerSystemItem parent);
    }
}


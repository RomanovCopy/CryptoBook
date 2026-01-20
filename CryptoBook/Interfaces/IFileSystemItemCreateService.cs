using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileSystemItemCreateService
    {
        IRootItem CreateRoot(string rootPath);
        IDirectoryItem CreateDirectory(string path, IContainerFileSystemItem parent);
        IFileItem CreateFile(string path, IContainerFileSystemItem parent);
        IContainerFileSystemItem CreateContainerDirectory(string path, IContainerFileSystemItem parent);
    }
}

}

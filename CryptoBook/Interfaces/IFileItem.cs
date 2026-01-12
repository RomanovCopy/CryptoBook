using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileItem:IFileSystemItem
    {
        long Size { get; }
        string Extension { get; }
    }
}

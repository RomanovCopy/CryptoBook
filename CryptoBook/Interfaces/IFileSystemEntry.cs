using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileSystemEntry: INode
    {
        string FullPath { get; set; }
        bool IsDirectory { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IVolume:INode
    {
        string RootPath { get; }
        string FileSystem { get; }
    }
}

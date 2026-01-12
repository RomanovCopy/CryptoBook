using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IRootItem
    {
        string RootPath { get; }          // "C:\"
        string VolumeLabel { get; }       // "System" (если доступно)
        string DriveFormat { get; }       // "NTFS" (если доступно)
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDriveItem:IContainerSystemItem
    {
        public string VolumeLabel { get; set; }
        public string DriveFormat { get; set; }
        public DriveType DriveType { get; set; }
        public long AvailableFreeSpace { get; set; }  // В байтах
        public long TotalSize { get; set; } // В байтах

    }
}

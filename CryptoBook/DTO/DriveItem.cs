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
    public class DriveItem: ContainerSystemItem, IDriveItem
    {
        public string VolumeLabel { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string DriveFormat { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DriveType DriveType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public long AvailableFreeSpace { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public long TotalSize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public DriveItem()
        {
        }

    }
}

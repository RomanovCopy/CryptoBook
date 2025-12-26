using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class DriveInfoEx
    {
        public string RootDirectory { get; set; }  // Например, "E:\"
        public string VolumeLabel { get; set; }
        public string DriveFormat { get; set; }
        public DriveType DriveType { get; set; }
        public long AvailableFreeSpace { get; set; }  // В байтах
        public long TotalSize { get; set; }          // В байтах

        public override string ToString()
        {
            return $"{RootDirectory} ({VolumeLabel}) - {DriveType}, {DriveFormat}, Свободно: {AvailableFreeSpace / (1024 * 1024 * 1024)} ГБ";
        }
    }
}

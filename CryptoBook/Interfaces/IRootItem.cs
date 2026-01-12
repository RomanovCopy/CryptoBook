using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IRootItem
    {
        string RootPath { get; }          // "C:\", "\\server\share\"
        string? VolumeLabel { get; }      // опционально
        bool IsReady { get; }             // для съемных/сетевых
    }
}

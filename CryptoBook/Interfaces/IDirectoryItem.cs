using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDirectoryItem:IContainerSystemItem
    {
        string Name { get; set; }
        ISystemItem? Parent { get; set; }

    }
}

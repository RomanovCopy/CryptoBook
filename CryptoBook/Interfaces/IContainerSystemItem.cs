using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IContainerSystemItem:ISystemItem
    {
        string Name { get; set; }
        bool IsLoaded { get; set; }
        bool IsExpanded { get; set; }
        ReadOnlyObservableCollection<ISystemItem> Children { get; }
        FileOperationResult AddChild(ISystemItem item);
        FileOperationResult RemoveChild(ISystemItem item);
    }
}

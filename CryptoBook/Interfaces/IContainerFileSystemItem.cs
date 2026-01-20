using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IContainerFileSystemItem
    {
        bool IsLoaded { get; }
        ReadOnlyObservableCollection<IFileSystemItem> Children { get; }
        FileOperationResult AddChild(IFileSystemItem item);
        FileOperationResult RemoveChild(IFileSystemItem item);
    }
}

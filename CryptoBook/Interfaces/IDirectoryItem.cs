using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDirectoryItem:IFileSystemItem
    {
        ReadOnlyObservableCollection<IFileSystemItem> Children { get; }
        bool IsLoaded { get; }
        Task EnsureLoadedAsync(CancellationToken ct = default);
        void Invalidate(); // пометить как не загруженную
    }
}

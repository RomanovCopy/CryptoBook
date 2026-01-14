using CryptoBook.DTO;

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
        ObservableCollection<IFileItem> Children { get; }
        bool IsLoaded { get; }

        // Загрузка / инвалидация
        Task EnsureLoadedAsync(CancellationToken ct = default);
        Task RefreshAsync(CancellationToken ct = default);  // перечитать содержимое

        // Мутации коллекции (UI-safe)
        Task AddChildAsync(IFileSystemItem item, CancellationToken ct = default);
        Task<bool> RemoveChildAsync(IFileSystemItem item, CancellationToken ct = default);
        Task ClearChildrenAsync(CancellationToken ct = default);

        // Операции файловой системы (на чистовую)
        Task<IFileItem> CreateFileAsync(string name, CancellationToken ct = default);
        Task<IDirectoryItem> CreateDirectoryAsync(string name, CancellationToken ct = default);
    }
}

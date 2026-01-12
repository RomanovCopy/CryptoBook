using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileItem:IFileSystemItem
    {
        long Size { get; }
        string Extension { get; }

        Task RenameAsync(string newName, CancellationToken ct = default);
        Task DeleteAsync(CancellationToken ct = default);
        Task MoveToAsync(IDirectoryItem targetDir, CancellationToken ct = default);
    }
}

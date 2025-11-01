using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFolderPickerService
    {
        // Вернёт нормализованный путь (например "local://C:/..") или null, если отменили.
        Task<string?> PickFolderAsync(string? initialDirectory, CancellationToken ct);
    }
}

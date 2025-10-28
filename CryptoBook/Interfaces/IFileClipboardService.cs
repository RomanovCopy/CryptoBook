using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileClipboardService:IService
    {
        // Поместить в буфер пути для копирования
        void SetCopy(IEnumerable<string> sourcePaths);

        // Поместить в буфер пути для перемещения (вырезать)
        void SetMove(IEnumerable<string> sourcePaths);

        // Вернуть текущие данные буфера
        ClipboardData GetData();

        // Очистить буфер явно (например, после успешного Cut+Paste)
        void Clear();

        // Вставить содержимое буфера в целевую папку
        Task<IReadOnlyList<FileOperationResult>> PasteAsync(
            string destinationDirectory,
            IProgressReporter? progress,
            CancellationToken cancellationToken);
    }
}

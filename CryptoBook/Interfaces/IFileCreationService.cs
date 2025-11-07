using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileCreationService:IService
    {
        // Подсказывает уникальное имя с учетом расширения и уже существующих файлов.
        Task<string> SuggestUniqueNameAsync(
            string targetDirectory,  // "local://C:/Temp"
            IFileTemplate template,
            CancellationToken ct);

        // Создает файл по шаблону.
        Task<FileOperationResult> CreateAsync(
            string targetDirectory,  // "local://C:/Temp"
            string fileNameWithOrWithoutExt, // "readme" или "readme.txt"
            IFileTemplate template,
            IfExistsMode ifExists,
            CancellationToken ct);
    }
}

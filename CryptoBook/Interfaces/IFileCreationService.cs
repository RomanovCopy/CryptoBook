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
        /// <summary>
        /// Подсказывает уникальное имя с учетом расширения и уже существующих файлов
        /// </summary>
        /// <param name="targetDirectory">директория</param>
        /// <param name="template">шаблон файла</param>
        /// <param name="ct">токен отмены</param>
        /// <returns></returns>
        Task<string> SuggestUniqueNameAsync(
            string targetDirectory,  // "local://C:/Temp"
            IFileTemplate template,
            CancellationToken ct);

        /// <summary>
        /// Создает файл по шаблону
        /// </summary>
        /// <param name="targetDirectory">директория</param>
        /// <param name="fileNameWithOrWithoutExt">имя файла с расширением или без</param>
        /// <param name="template">шаблон файла</param>
        /// <param name="ifExists">что делать если файл существует</param>
        /// <param name="isHidden">скрытый</param>
        /// <param name="isHidden">только для чтения</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<FileOperationResult> CreateAsync(
            string targetDirectory,  // "local://C:/Temp"
            string fileNameWithOrWithoutExt, // "readme" или "readme.txt"
            IFileTemplate template,
            IfExistsMode ifExists,
            bool isHidden,
            bool isReadOnly,
            CancellationToken ct);
    }
}

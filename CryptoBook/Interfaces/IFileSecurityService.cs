using CryptoBook.DTO;
using CryptoBook.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileSecurityService
    {
        /// <summary>
        /// Шифрование файла/каталога
        /// </summary>
        /// <param name="source">Источник</param>
        /// <param name="destinationPath">Цель</param>
        /// <param name="mode">режим шифрования</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        public Task<FileOperationResult> EncryptAsync(ISystemItem source, string destinationPath, EncryptionTargetMode mode, 
        IProgressReporter? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Шифрует несколько файлов и каталогов с заменой исходных элементов.
        /// </summary>
        Task<FileOperationBatchResult> EncryptAsync(
            IReadOnlyList<ISystemItem> sources,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Деифрование файла/каталога
        /// </summary>
        /// <param name="source">Источник</param>
        /// <param name="destinationPath">Цель</param>
        /// <param name="mode">режим шифрования</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        Task<FileOperationResult> DecryptAsync(ISystemItem source, string destinationPath, EncryptionTargetMode mode, 
        IProgressReporter? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Расшифровывает несколько файлов и каталогов с заменой исходных элементов.
        /// </summary>
        Task<FileOperationBatchResult> DecryptAsync(
            IReadOnlyList<ISystemItem> sources,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Расшифровывает несколько файлов и каталогов в выбранный каталог,
        /// сохраняя защищённые исходники. Конфликтующие имена не перезаписываются.
        /// </summary>
        Task<FileOperationBatchResult> DecryptAsync(
            IReadOnlyList<ISystemItem> sources,
            string destinationDirectory,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);

    }
}

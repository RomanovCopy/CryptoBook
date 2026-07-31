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
        /// Деифрование файла/каталога
        /// </summary>
        /// <param name="source">Источник</param>
        /// <param name="destinationPath">Цель</param>
        /// <param name="mode">режим шифрования</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        Task<FileOperationResult> DecryptAsync(ISystemItem source, string destinationPath, EncryptionTargetMode mode, 
        IProgressReporter? progress = null, CancellationToken cancellationToken = default);

    }
}

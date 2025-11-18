using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileManagerService:IService
    {
        // Унифицированное перечисление содержимого каталога
        Task<DirectoryContent> BrowseAsync(string path,  CancellationToken ct, bool includeHidden=false);

        // Копирование (одного файла/папки) с прогрессом
        Task<FileOperationResult> CopyAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken);

        Task<FileOperationResult> MoveAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken);

        Task<FileOperationResult> DeleteAsync(
            string path,
            CancellationToken cancellationToken);

        Task<FileOperationResult> RenameAsync(
            string path,
            string newName,
            CancellationToken cancellationToken);

        Task<FileOperationResult> CreateDirectoryAsync(
            string parentDirectory,
            string newDirectoryName,
            CancellationToken cancellationToken);

        // Проверки доступа
        Task<bool> CanReadAsync(string path, CancellationToken cancellationToken);
        Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken);

        // Потоки для превью/редактирования
        Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken);
        Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken);

        Task<bool> IsHiddenAsync(string path, CancellationToken cancellationToken);
        Task<FileOperationResult> SetHiddenAsync(string path, bool hidden, CancellationToken cancellationToken);
        Task<bool> IsReadOnlyAsync(string path, CancellationToken cancellationToken);
        public Task<FileOperationResult> SetReadOnlyAsync(string path, bool isReadOnly, CancellationToken ct);

        // Нормализация путей
        string NormalizePath(string rawPath);

    }
}

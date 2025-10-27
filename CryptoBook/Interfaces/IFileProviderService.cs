using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileProviderService
    {
        // Тип провайдера, например "local", "zip", "ssh"
        string Scheme { get; }

        // Чтение содержимого директории
        Task<IDirectoryContent> GetDirectoryContentAsync( string path, CancellationToken cancellationToken);

        // Создать поток для чтения файла
        Task<Stream> OpenReadAsync( string path, CancellationToken cancellationToken);

        // Создать поток для записи (перезапись файла)
        Task<Stream> OpenWriteAsync( string path, bool overwrite, CancellationToken cancellationToken);

        // Копирование одного файла или директории (c прогрессом)
        Task<IFileOperationResult> CopyAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken);

        // Перемещение (обычно rename/move)
        Task<IFileOperationResult> MoveAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken);

        // Удаление файла или директории (рекурсивно для директорий)
        Task<IFileOperationResult> DeleteAsync(
            string path,
            CancellationToken cancellationToken);

        // Переименование (в рамках одной папки)
        Task<IFileOperationResult> RenameAsync(
            string path,
            string newName,
            CancellationToken cancellationToken);

        // Проверка доступа (чтение/запись)
        Task<bool> CanReadAsync(string path, CancellationToken cancellationToken);
        Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken);

        // Создать каталог
        Task<IFileOperationResult> CreateDirectoryAsync(
            string directoryPath,
            CancellationToken cancellationToken);
    }
}

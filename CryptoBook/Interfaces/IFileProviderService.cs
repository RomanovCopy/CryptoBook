using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileProviderService: IService
    {
        /// <summary>
        /// Тип провайдера, например "local", "zip", "ssh"
        /// </summary>
        string Scheme { get; }

        /// <summary>
        /// Чтение содержимого директории
        /// </summary>
        /// <param name="path">путь к директории</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <param name="includeHidden">показывать скрытые файлы</param>
        /// <returns></returns>
        Task<List<FileItem>> GetDirectoryContentAsync( string path, CancellationToken cancellationToken, bool includeHidden = false);

        /// <summary>
        /// Создать поток для чтения файла
        /// </summary>
        /// <param name="path">путь к файлу</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<Stream> OpenReadAsync( string path, CancellationToken cancellationToken);

        /// <summary>
        /// Создать поток для записи (перезапись файла)
        /// </summary>
        /// <param name="path">путь к файлу</param>
        /// <param name="overwrite">перезаписать</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<Stream> OpenWriteAsync( string path, bool overwrite, CancellationToken cancellationToken);

        /// <summary>
        /// Копирование одного файла или директории (c прогрессом)
        /// </summary>
        /// <param name="sourcePath">путь к источнику</param>
        /// <param name="destinationPath">путь к месту назначения</param>
        /// <param name="progress">проресс выполнения операции</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<FileOperationResult> CopyAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// Перемещение (обычно rename/move)
        /// </summary>
        /// <param name="sourcePath">путь к источнику</param>
        /// <param name="destinationPath">путь к месту назначения</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<FileOperationResult> MoveAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken);

        /// <summary>
        /// Удаление файла или директории (рекурсивно для директорий)
        /// </summary>
        /// <param name="path">путь к файлу или директории</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<FileOperationResult> DeleteAsync(
            string path,
            CancellationToken cancellationToken);

        /// <summary>
        /// Переименование (в рамках одной папки)
        /// </summary>
        /// <param name="path">путь</param>
        /// <param name="newName">новое имя</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<FileOperationResult> RenameAsync(
            string path,
            string newName,
            CancellationToken cancellationToken);

        /// <summary>
        /// проверка доступа для чтения
        /// </summary>
        /// <param name="path">путь</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<bool> CanReadAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// Проверка доступа для записи
        /// </summary>
        /// <param name="path">путь</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// Создать каталог
        /// </summary>
        /// <param name="directoryPath">путь к директории</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<FileOperationResult> CreateDirectoryAsync(
            string directoryPath,
            CancellationToken cancellationToken);

        /// <summary>
        /// файл только для чтения
        /// </summary>
        /// <param name="path">путь</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<bool> IsReadOnlyAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// файл скрытый
        /// </summary>
        /// <param name="path">путь</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<bool> IsHiddenAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// сделать файл скрытым
        /// </summary>
        /// <param name="path">путь</param>
        /// <param name="hidden">сделать файл скрытым</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns></returns>
        Task<FileOperationResult> SetHiddenAsync(string path, bool hidden, CancellationToken cancellationToken);
        Task<FileOperationResult> SetReadOnlyAsync(string path, bool isReadOnly, CancellationToken cancellationToken);
    }
}

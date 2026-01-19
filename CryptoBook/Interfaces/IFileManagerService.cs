using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Сервис для работы с файловой системой: перечисление, операции над файлами/папками и управление атрибутами.
    /// </summary>
    public interface IFileManagerService:IService
    {
        /// <summary>
        /// Унифицированное перечисление содержимого каталога.
        /// </summary>
        /// <param name="path">Путь к каталогу для просмотра. Может быть абсолютным или относительным.</param>
        /// <param name="ct">Токен отмены для прерывания операции.</param>
        /// <param name="includeHidden">Включать ли скрытые файлы и папки в результат.</param>
        /// <returns>Объект <see cref="DirectoryContent"/>, содержащий элементы каталога и метаданные.</returns>
        /// <exception cref="DirectoryNotFoundException">Если каталог не найден.</exception>
        /// <exception cref="UnauthorizedAccessException">Если нет прав доступа к каталогу.</exception>
        Task<List<IFileSystemItem>> BrowseAsync(string path,  CancellationToken ct, bool includeHidden=false);

        /// <summary>
        /// Копирование одного файла или папки с поддержкой отчёта о прогрессе.
        /// </summary>
        /// <param name="sourcePath">Исходный путь файла или папки.</param>
        /// <param name="destinationPath">Путь назначения (файл или папка).</param>
        /// <param name="progress">Опциональный объект для получения прогресса операции.</param>
        /// <param name="cancellationToken">Токен отмены для прерывания операции.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        /// <remarks>Операция должна корректно обрабатывать отмену и частичные результаты в случае ошибки.</remarks>
        Task<FileOperationResult> CopyAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// Перемещение (перенос) файла или папки.
        /// </summary>
        /// <param name="sourcePath">Исходный путь.</param>
        /// <param name="destinationPath">Путь назначения.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        Task<FileOperationResult> MoveAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken);

        /// <summary>
        /// Удаление файла или папки по указанному пути.
        /// </summary>
        /// <param name="path">Путь к удаляемому объекту.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        Task<FileOperationResult> DeleteAsync(
            string path,
            CancellationToken cancellationToken);

        /// <summary>
        /// Переименование файла или папки.
        /// </summary>
        /// <param name="path">Текущий путь к объекту.</param>
        /// <param name="newName">Новое имя (без пути).</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        Task<FileOperationResult> RenameAsync(
            string path,
            string newName,
            CancellationToken cancellationToken);

        /// <summary>
        /// Создаёт новую поддиректорию внутри указанного родительского каталога.
        /// </summary>
        /// <param name="parentDirectory">Путь к родительскому каталогу.</param>
        /// <param name="newDirectoryName">Имя создаваемой директории.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        Task<FileOperationResult> CreateDirectoryAsync(
            string parentDirectory,
            string newDirectoryName,
            CancellationToken cancellationToken);

        // Проверки доступа

        /// <summary>
        /// Проверяет, есть ли у текущего пользователя доступ на чтение указанного пути.
        /// </summary>
        /// <param name="path">Проверяемый путь.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>True — доступ на чтение разрешён; иначе false.</returns>
        Task<bool> CanReadAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// Проверяет, есть ли у текущего пользователя доступ на запись в указанный путь.
        /// </summary>
        /// <param name="path">Проверяемый путь.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>True — доступ на запись разрешён; иначе false.</returns>
        Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken);

        // Потоки для превью/редактирования

        /// <summary>
        /// Открывает поток для чтения указанного файла.
        /// </summary>
        /// <param name="path">Путь к файлу для чтения.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Асинхронно возвращает поток <see cref="Stream"/>. Владелец потока обязан его освободить.</returns>
        /// <exception cref="FileNotFoundException">Если файл не найден.</exception>
        Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// Открывает поток для записи в файл.
        /// </summary>
        /// <param name="path">Путь к файлу для записи.</param>
        /// <param name="overwrite">Если true — перезаписывать существующий файл; иначе — генерировать ошибку при существовании.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Асинхронно возвращает поток <see cref="Stream"/> для записи. Владелец потока обязан его освободить.</returns>
        Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken);

        /// <summary>
        /// Проверяет, помечен ли файл или папка как скрытый.
        /// </summary>
        /// <param name="path">Путь к объекту.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>True — объект скрыт; иначе false.</returns>
        Task<bool> IsHiddenAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// Устанавливает или снимает скрытый атрибут у файла или папки.
        /// </summary>
        /// <param name="path">Путь к объекту.</param>
        /// <param name="hidden">True — установить скрытый атрибут; false — снять.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        Task<FileOperationResult> SetHiddenAsync(string path, bool hidden, CancellationToken cancellationToken);

        /// <summary>
        /// Проверяет, установлен ли атрибут "только для чтения" у файла или папки.
        /// </summary>
        /// <param name="path">Путь к объекту.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>True — объект доступен только для чтения; иначе false.</returns>
        Task<bool> IsReadOnlyAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// Устанавливает или снимает атрибут "только для чтения" у файла или папки.
        /// </summary>
        /// <param name="path">Путь к объекту.</param>
        /// <param name="isReadOnly">True — установить только для чтения; false — снять атрибут.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Результат операции в виде <see cref="FileOperationResult"/>.</returns>
        public Task<FileOperationResult> SetReadOnlyAsync(string path, bool isReadOnly, CancellationToken ct);

        // Нормализация путей

        /// <summary>
        /// Нормализует входной путь: убирает лишние разделители, разрешает относительные сегменты и приводит разделители к единому виду.
        /// </summary>
        /// <param name="rawPath">Исходный (сырый) путь.</param>
        /// <returns>Нормализованный путь.</returns>
        string NormalizePath(string rawPath);

    }
}

using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class FileProviderService:IFileProviderService
    {
        public string Scheme => "local";


        /// <summary>
        /// Возвращает список файлов/подкаталогов внутри заданной директории.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="cancellationToken">адрес директории</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">токен отмены операции</exception>
        public async Task<DirectoryContent> GetDirectoryContentAsync(string path, CancellationToken cancellationToken)
        {
            // В локальной ФС это синхронно и очень быстро,
            // но мы оборачиваем в Task.Run чтобы не блокировать UI-поток,
            // и чтобы это было консистентно с другими провайдерами (ssh и т.д.).
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dirInfo = new DirectoryInfo(path);
                if(!dirInfo.Exists)
                    throw new DirectoryNotFoundException($"Directory not found: {path}");

                // Сначала каталоги
                var directories = dirInfo.EnumerateDirectories()
                    .Select(d => ToFileItem(d))
                    .ToList();

                cancellationToken.ThrowIfCancellationRequested();

                // Потом файлы
                var files = dirInfo.EnumerateFiles()
                    .Select(f => ToFileItem(f))
                    .ToList();

                var all = directories.Concat(files).ToList();

                return new DirectoryContent
                {
                    DirectoryPath = path,
                    Items = all
                };
            }, cancellationToken);
        }

        /// <summary>
        /// Открыть поток только для чтения.
        /// Ответственность закрыть Stream лежит на вызывающем коде
        /// </summary>
        /// <param name="path"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
        {
            // Здесь можно сразу вернуть Task.FromResult, но мы аккуратны с отменой:
            cancellationToken.ThrowIfCancellationRequested();

            // FileStream в async-режиме (useAsync: true) позволяет читать неблокирующе.
            Stream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            return Task.FromResult(stream);
        }

        /// <summary>
        /// Открыть поток для записи.    
        /// </summary>
        /// <param name="path"></param>
        /// <param name="overwrite"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if(File.Exists(path) && !overwrite)
            {
                throw new IOException($"File already exists and overwrite=false: {path}");
            }

            Stream stream = new FileStream(
                path,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            return Task.FromResult(stream);
        }

        /// <summary>
        /// Копирование файла или директории (рекурсивно).     
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        /// <param name="progress"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<FileOperationResult> CopyAsync(string sourcePath, string destinationPath, IProgressReporter? progress, CancellationToken cancellationToken)
        {
            try
            {
                // Папка?
                if(Directory.Exists(sourcePath))
                {
                    await CopyDirectoryRecursiveAsync(
                        sourcePath,
                        destinationPath,
                        progress,
                        cancellationToken);

                    return FileOperationResult.Ok();
                }

                // Файл?
                if(File.Exists(sourcePath))
                {
                    await CopyFileAsync(
                        sourcePath,
                        destinationPath,
                        progress,
                        cancellationToken);

                    return FileOperationResult.Ok();
                }

                return FileOperationResult.Fail("Source not found.");
            } catch(OperationCanceledException)
            {
                return FileOperationResult.Fail("Operation canceled.");
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Перемещение (move/rename через System.IO).     
        /// Для директорий поддерживаем Directory.Move,
        /// для файлов — File.Move.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<FileOperationResult> MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(Directory.Exists(sourcePath))
                {
                    // Directory.Move поддерживает перемещение и переименование.
                    await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Directory.Move(sourcePath, destinationPath);
                    }, cancellationToken);

                    return FileOperationResult.Ok();
                }

                if(File.Exists(sourcePath))
                {
                    await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        File.Move(sourcePath, destinationPath);
                    }, cancellationToken);

                    return FileOperationResult.Ok();
                }

                return FileOperationResult.Fail("Source not found.");
            } catch(OperationCanceledException)
            {
                return FileOperationResult.Fail("Operation canceled.");
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Удаление файла или папки (папка — рекурсивно).     
        /// </summary>
        /// <param name="path"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<FileOperationResult> DeleteAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if(Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    } else if(File.Exists(path))
                    {
                        File.Delete(path);
                    } else
                    {
                        throw new FileNotFoundException("Path not found.", path);
                    }
                }, cancellationToken);

                return FileOperationResult.Ok();
            } catch(OperationCanceledException)
            {
                return FileOperationResult.Fail("Operation canceled.");
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Переименование без смены родительской директории.      
        /// Реализовано как Move в ту же папку с новым именем.      
        /// </summary>
        /// <param name="path"></param>
        /// <param name="newName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<FileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? parentDir = Path.GetDirectoryName(path);
                if(string.IsNullOrWhiteSpace(parentDir))
                    return FileOperationResult.Fail("Cannot determine parent directory.");

                string newPath = Path.Combine(parentDir, newName);

                return await MoveAsync(path, newPath, cancellationToken);
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Пробуем открыть на чтение — если получилось, значит можем читать.      
        /// </summary>
        /// <param name="path"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> CanReadAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(Directory.Exists(path))
                {
                    // Проверим, можем ли мы перечислить хотя бы 1 элемент
                    return await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            _ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault();
                            return true;
                        } catch
                        {
                            return false;
                        }
                    }, cancellationToken);
                }

                if(File.Exists(path))
                {
                    await using var _ = await OpenReadAsync(path, cancellationToken);
                    return true;
                }

                return false;
            } catch
            {
                return false;
            }
        }

        /// <summary>
        /// Пробуем создать временный файл / открыть на запись.
        /// Для файла — пытаемся открыть FileStream с FileAccess.Write.
        /// Для директории — создаем временный файл и сразу удаляем.      
        /// </summary>
        /// <param name="path"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if(Directory.Exists(path))
                {
                    return await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string testFile = System.IO.Path.Combine(path, System.IO.Path.GetRandomFileName());
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                        return true;
                    }, cancellationToken);
                }

                if(File.Exists(path))
                {
                    await using var fs = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        useAsync: true);

                    return true;
                }

                // путь не существует:
                // проверим, можем ли мы создать такой файл
                string? parentDir = Path.GetDirectoryName(path);
                if(string.IsNullOrWhiteSpace(parentDir) || !Directory.Exists(parentDir))
                    return false;

                string tmp = System.IO.Path.Combine(parentDir, System.IO.Path.GetRandomFileName());
                File.WriteAllText(tmp, "test");
                File.Delete(tmp);
                return true;
            } catch
            {
                return false;
            }
        }

        /// <summary>
        /// Создание директории (если уже есть — это не ошибка).       
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<FileOperationResult> CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(directoryPath);
                }, cancellationToken);

                return FileOperationResult.Ok();
            } catch(OperationCanceledException)
            {
                return FileOperationResult.Fail("Operation canceled.");
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }



        // --------------------------
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // --------------------------

        private static FileItem ToFileItem(FileSystemInfo info)
        {
            bool isDir = info is DirectoryInfo;

            long? size = null;
            if(!isDir && info is FileInfo fi)
            {
                size = fi.Length;
            }

            bool isHidden = (info.Attributes & FileAttributes.Hidden) != 0;
            bool isReadOnly = (info.Attributes & FileAttributes.ReadOnly) != 0;

            return new FileItem
            {
                FullPath = info.FullName,
                Name = info.Name,
                IsDirectory = isDir,
                SizeBytes = size,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                IsHidden = isHidden,
                IsReadOnly = isReadOnly
            };
        }

        private async Task CopyFileAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            // Гарантируем наличие директории назначения
            string? destDir = Path.GetDirectoryName(destinationPath);
            if(!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            // Читаем поблочно и вручную считаем прогресс.
            const int BufferSize = 1024 * 64;
            byte[] buffer = new byte[BufferSize];

            var sourceInfo = new FileInfo(sourcePath);
            long totalBytes = sourceInfo.Length;
            long copiedBytes = 0;

            await using var sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                useAsync: true);

            await using var destStream = new FileStream(
                destinationPath,
                FileMode.Create, // перезапишем если было
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int read = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if(read == 0)
                    break;

                await destStream.WriteAsync(buffer, 0, read, cancellationToken);

                copiedBytes += read;

                if(progress != null && totalBytes > 0)
                {
                    double ratio = (double)copiedBytes / totalBytes;
                    progress.Report(ratio, $"Copying {Path.GetFileName(sourcePath)}");
                }
            }

            progress?.Report(1.0, $"Done {Path.GetFileName(sourcePath)}");
        }

        private async Task CopyDirectoryRecursiveAsync(
            string sourceDir,
            string destDir,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Создаем корневую директорию назначения
            Directory.CreateDirectory(destDir);

            // Копируем все файлы в этой директории
            foreach(string filePath in Directory.EnumerateFiles(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(destDir, fileName);

                // Для директорий прогресс "недетерминированный" — мы честно репортим null
                progress?.Report(null, $"Copying {fileName}");

                await CopyFileAsync(filePath, destFilePath, progress, cancellationToken);
            }

            // Рекурсивно в подкаталоги
            foreach(string subDir in Directory.EnumerateDirectories(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string dirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(destDir, dirName);

                await CopyDirectoryRecursiveAsync(subDir, destSubDir, progress, cancellationToken);
            }
        }
    }
}

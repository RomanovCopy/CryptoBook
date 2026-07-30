using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using Mono.Unix;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class FileProviderService: IFileProviderService
    {
        private readonly ISystemItemCreateService _itemCreateService;
        private readonly ISecureFileValidator _secureFileValidator;
        private readonly IKeyProvider _keyProvider;
        private readonly ISecureFileProcessor _secureFileProcessor;

        private object _lock = new object();
        public string Scheme => "local";

        public FileProviderService(ISystemItemCreateService? itemCreateService, ISecureFileValidator? secureFileValidator, IKeyProvider? keyProvider, ISecureFileProcessor? secureFileProcessor)
        {
            _itemCreateService = itemCreateService ?? throw new ArgumentNullException(nameof(itemCreateService));
            _secureFileValidator = secureFileValidator ?? throw new ArgumentNullException(nameof(secureFileValidator));
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _secureFileProcessor = secureFileProcessor ?? throw new ArgumentNullException(nameof(secureFileProcessor));
        }

        /// <summary>
        /// Возвращает список файлов/подкаталогов внутри заданной директории.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="cancellationToken">адрес директории</param>
        /// <returns></returns>
        public async Task<List<ISystemItem>> GetContainerContentAsync(string path, IProgressReporter? progress = null, CancellationToken cancellationToken = default, bool includeHidden = false)
        {
            try
            {
                return await Task.Run(() =>
                {
                    lock(_lock)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var directories = new List<ISystemItem>();
                        var dirInfo = new DirectoryInfo(path);
                        if(!dirInfo.Exists)
                            throw new DirectoryNotFoundException(path);

                        // Все найденные элементы получают одно представление текущего каталога как родителя.
                        var parentItem = ToFileItem(dirInfo);

                        foreach(var d in dirInfo.EnumerateDirectories())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if(includeHidden || !IsFileSystemInfoHidden(d))
                            {
                                if(CanAccess(d.FullName))
                                    directories.Add(_itemCreateService.CreateDirectory(d.FullName, parentItem));
                            }
                        }

                        var files = new List<ISystemItem>();
                        foreach(var f in dirInfo.EnumerateFiles())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if(includeHidden || !IsFileSystemInfoHidden(f))
                                files.Add(_itemCreateService.CreateFile(f.FullName, parentItem));
                        }
                        var allItems = directories.Concat(files).ToList();
                        return allItems;
                    }
                }, cancellationToken);
            } catch(OperationCanceledException)
            {
                throw;
            } catch(DirectoryNotFoundException)
            {
                throw;
            } catch(UnauthorizedAccessException ex)
            {
                throw new IOException($"Access denied while enumerating '{path}'", ex);
            } catch(Exception ex)
            {
                throw new IOException($"Failed to enumerate directory '{path}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Открыть поток только для чтения.
        /// Ответственность закрыть Stream лежит на вызывающем коде
        /// </summary>
        /// <param name="path"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Stream> OpenReadAsync(string path, IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var encrypted = await _secureFileValidator.HasCryptoBookHeaderAsync(path, cancellationToken);

                if(encrypted)
                {
                    if(_keyProvider.HasKey)
                    {
                        return await _secureFileProcessor.DecryptFileAsyncToStream(path, progress, cancellationToken);
                    } else
                    {
                        throw new CryptographicException($"File is encrypted and cannot be opened for reading: {path}");
                    }

                } else
                {
                    // FileStream в async-режиме (useAsync: true) позволяет читать неблокирующе.
                    Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

                    return Task.FromResult(stream).Result;
                }

            } catch(OperationCanceledException) { throw; } catch(FileNotFoundException)
            {
                throw; // пробрасываем дальше, чтобы можно было различать "файл не найден" и "другая ошибка"
            } catch(Exception ex)
            {
                throw new IOException($"Cannot open file for reading: {path}", ex);
            }
        }

        /// <summary>
        /// Открыть поток для записи.    
        /// </summary>
        /// <param name="path">путь к существующему или новому файлу</param>
        /// <param name="overwrite">следует-ли осуществлять перезапись файла</param>
        /// <param name="cancellationToken">токен отменя операции</param>
        /// <returns></returns>
        public Task<Stream> OpenWriteAsync(string path, bool overwrite, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if(File.Exists(path) && !overwrite)
            {
                throw new IOException($"File already exists and overwrite=false: {path}");
            }

            try
            {
                if(_keyProvider.HasKey)
                {
                    //_secureFileProcessor.EncryptFileAsync(path, overwrite, progress, cancellationToken);


                }
                else
                {

                }


                Stream stream = new FileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true);
                

                return Task.FromResult(stream);
            } catch(OperationCanceledException) { throw; } catch(Exception ex)
            {
                throw new IOException($"Cannot open file for writing: {path}", ex);
            }
        }

        /// <summary>
        /// Копирование файла или директории (рекурсивно).     
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        /// <param name="progress"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<FileOperationResult> CopyAsync(string sourcePath, string destinationPath, IProgressReporter? progress, CancellationToken cancellationToken)
        {
            try
            {
                // Папка?
                if(Directory.Exists(sourcePath))
                {
                    await CopyDirectoryRecursiveAsync(sourcePath, destinationPath, progress, cancellationToken);

                    return FileOperationResult.Ok();
                }

                // Файл?
                if(File.Exists(sourcePath))
                {
                    await CopyFileAsync(sourcePath, destinationPath, progress, cancellationToken);

                    return FileOperationResult.Ok();
                }

                return FileOperationResult.Fail("Source not found.");
            } catch(OperationCanceledException)
            {
                throw;
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
        public async Task<FileOperationResult> MoveAsync(string sourcePath, string destinationPath, IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(Directory.Exists(sourcePath))
                {
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
                throw;
            } catch(System.IO.IOException ex) when(ex.Message.Contains("already exists"))
            {
                return FileOperationResult.Fail("Destination already exists.");
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
                throw;
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
        public async Task<FileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? parentDir = Path.GetDirectoryName(path);
                if(string.IsNullOrWhiteSpace(parentDir))
                    return FileOperationResult.Fail("Cannot determine parent directory.");

                string newPath = Path.Combine(parentDir, newName);

                return await MoveAsync(path, newPath, null, cancellationToken);
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
                        } catch(OperationCanceledException)
                        {
                            throw;
                        } catch
                        {
                            return false;
                        }
                    }, cancellationToken);
                }

                if(File.Exists(path))
                {
                    await using var _ = await OpenReadAsync(path, null, cancellationToken);
                    return true;
                }

                return false;
            } catch(OperationCanceledException)
            {
                throw;
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
            } catch(OperationCanceledException)
            {
                throw;
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
                throw;
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        public async Task<bool> IsReadOnlyAsync(string path, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    return fileInfo.IsReadOnly;
                }

                if(Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    try
                    {
                        // Проверяем, можно ли создать временный файл
                        string testFile = Path.Combine(dirInfo.FullName, Path.GetRandomFileName());
                        using(File.Create(testFile, 1, FileOptions.DeleteOnClose))
                        { }
                        return false;
                    } catch(UnauthorizedAccessException)
                    {
                        return true;
                    }
                }

                throw new FileNotFoundException(path);
            }, cancellationToken);
        }


        public async Task<bool> IsHiddenAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                if(Directory.Exists(path) || File.Exists(path))
                {
                    FileSystemInfo fsi = GetFileSystemInfo(path);
                    return IsFileSystemInfoHidden(fsi);
                }
                throw new FileNotFoundException(path);
            }, cancellationToken);
        }

        public async Task<FileOperationResult> SetHiddenAsync(string path, bool hidden, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var fsi = Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path) as FileSystemInfo;
                    if(fsi == null)
                        return FileOperationResult.Fail("Path not found.");

                    await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var attrs = fsi.Attributes;
                        if(hidden)
                            fsi.Attributes = attrs | FileAttributes.Hidden;
                        else
                            fsi.Attributes = attrs & ~FileAttributes.Hidden;
                    }, cancellationToken);

                    return FileOperationResult.Ok();
                } else
                {
                    // Unix-like: скрытость по имени. Переименование.
                    if(!File.Exists(path) && !Directory.Exists(path))
                        return FileOperationResult.Fail("Path not found.");

                    string dir = Path.GetDirectoryName(path) ?? "";
                    string name = Path.GetFileName(path);

                    bool currentlyHidden = name.StartsWith('.');
                    if(hidden == currentlyHidden)
                        return FileOperationResult.Ok();

                    string newName = hidden ? "." + name : name.TrimStart('.');
                    string newPath = Path.Combine(dir, newName);

                    // аккуратно: проверяем, не существует ли уже newPath
                    if(File.Exists(newPath) || Directory.Exists(newPath))
                        return FileOperationResult.Fail("Target name already exists.");

                    await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Directory.Move(path, newPath); // Directory.Move работает для файлов и каталогов
                    }, cancellationToken);

                    return FileOperationResult.Ok();
                }
            } catch(OperationCanceledException) { throw; } catch(Exception ex) { return FileOperationResult.Fail(ex.Message); }
        }

        public async Task<FileOperationResult> SetReadOnlyAsync(string path, bool isReadOnly, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var fsi = Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path) as FileSystemInfo;
                    if(fsi == null)
                        return FileOperationResult.Fail("Path not found.");

                    await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var attrs = fsi.Attributes;
                        if(isReadOnly)
                            fsi.Attributes = attrs | FileAttributes.ReadOnly;
                        else
                            fsi.Attributes = attrs & ~FileAttributes.ReadOnly;
                    }, cancellationToken);

                    return FileOperationResult.Ok();
                } else
                {
                    // Linux / macOS: используем POSIX-права
                    return await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var unixFile = new UnixFileInfo(path);
                        var perms = unixFile.FileAccessPermissions;

                        if(isReadOnly)
                        {
                            // Убираем право записи владельцу
                            perms &= ~FileAccessPermissions.UserWrite;
                        } else
                        {
                            // Возвращаем UserWrite
                            perms |= FileAccessPermissions.UserWrite;
                        }

                        unixFile.FileAccessPermissions = perms;
                        return FileOperationResult.Ok();

                    }, cancellationToken);
                }
            } catch(OperationCanceledException)
            {
                throw;
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }



        // --------------------------
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // --------------------------

        private bool IsReparsePoint(DirectoryInfo dir) => dir.Attributes.HasFlag(FileAttributes.ReparsePoint);

        private bool CanAccess(string path)
        {
            try
            {
                Directory.EnumerateFileSystemEntries(path).FirstOrDefault();
                return true;
            } catch(UnauthorizedAccessException)
            {
                return false;
            } catch(Exception)
            {
                return false;
            }
        }


        private ISystemItem ToFileItem(FileSystemInfo info)
        {
            if(info is DirectoryInfo d)
            {
                if(d.Parent == null)
                {
                    return _itemCreateService.CreateRoot(d.Root.Name);
                } else
                {
                    return _itemCreateService.CreateDirectory(d.FullName,
                        d.Parent != null ? ToFileItem(d.Parent) : null);
                }
            } else if(info is FileInfo f)
            {
                var path = f.FullName;
                var parent = new FileInfo(path).Directory;
                return _itemCreateService.CreateFile(f.FullName,
                    Path.GetDirectoryName(f.FullName) != null ? ToFileItem(parent) : null);
            } else
                throw new InvalidOperationException("Unknown FileSystemInfo type.");
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

        private async Task CopyDirectoryRecursiveAsync(string sourceDir, string destDir, IProgressReporter? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint,
                ReturnSpecialDirectories = false
            };
            string[] files = Directory.GetFiles(sourceDir, "*", options);
            string[] directories = Directory.GetDirectories(sourceDir, "*", options);
            long totalBytes = files.Sum(path => new FileInfo(path).Length);
            long completedBytes = 0;

            Directory.CreateDirectory(destDir);
            foreach(string directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.Combine(
                    destDir,
                    Path.GetRelativePath(sourceDir, directory)));
            }

            foreach(string filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long fileSize = new FileInfo(filePath).Length;
                string destination = Path.Combine(
                    destDir,
                    Path.GetRelativePath(sourceDir, filePath));

                IProgressReporter? fileProgress = progress is null
                    ? null
                    : new CopyProgressReporter(
                        progress,
                        completedBytes,
                        fileSize,
                        totalBytes,
                        Path.GetFileName(filePath));

                await CopyFileAsync(filePath, destination, fileProgress, cancellationToken);
                completedBytes += fileSize;
            }

            progress?.Report(1.0, "Копирование завершено");
        }

        private sealed class CopyProgressReporter: IProgressReporter
        {
            private readonly IProgressReporter _outer;
            private readonly long _completedBytes;
            private readonly long _fileSize;
            private readonly long _totalBytes;
            private readonly string _fileName;

            public CopyProgressReporter(
                IProgressReporter outer,
                long completedBytes,
                long fileSize,
                long totalBytes,
                string fileName)
            {
                _outer = outer;
                _completedBytes = completedBytes;
                _fileSize = fileSize;
                _totalBytes = totalBytes;
                _fileName = fileName;
            }

            public void Report(double? value, string? currentInfo = null)
            {
                if(value is null || _totalBytes == 0)
                {
                    _outer.Report(value, currentInfo ?? _fileName);
                    return;
                }

                double overall = (_completedBytes + _fileSize * value.Value) / _totalBytes;
                _outer.Report(Math.Clamp(overall, 0.0, 1.0), currentInfo ?? _fileName);
            }
        }

        private static bool IsFileSystemInfoHidden(FileSystemInfo info)
        {
            // Windows: атрибут Hidden
            try
            {
                if(Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    return (info.Attributes & FileAttributes.Hidden) != 0;
                } else
                {
                    // Unix: скрытый — имя начинается с '.'
                    return info.Name.StartsWith('.');
                }
            } catch
            {
                // В случае проблем — не считать скрытым, чтобы не ломать UX
                return false;
            }
        }

        private static FileSystemInfo GetFileSystemInfo(string path)
        {
            if(File.Exists(path))
                return new FileInfo(path);
            if(Directory.Exists(path))
                return new DirectoryInfo(path);
            throw new FileNotFoundException($"Путь не найден: {path}");
        }
    }

}

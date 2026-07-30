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
    /// <summary>
    /// Внутренний файловый буфер обмена (НЕ системный).
    /// Поддерживает режим Copy и Move.
    /// </summary>
    public sealed class FileClipboardService:IFileClipboardService
    {
        private readonly IFileManagerService _fileManager;
        private ClipboardData _clipboard = new();
        private readonly object _sync = new();

        public FileClipboardService(IFileManagerService fileManager)
        {
            _fileManager = fileManager;
        }

        public void SetCopy(IEnumerable<string> sourcePaths)
        {
            if(sourcePaths is null)
                throw new ArgumentNullException(nameof(sourcePaths));

            var list = sourcePaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            lock(_sync)
            {
                _clipboard = new ClipboardData
                {
                    SourcePaths = list,
                    Operation = ClipboardOperationKind.Copy,
                    TimestampUtc = DateTime.UtcNow
                };
            }
        }

        public void SetMove(IEnumerable<string> sourcePaths)
        {
            if(sourcePaths is null)
                throw new ArgumentNullException(nameof(sourcePaths));

            var list = sourcePaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            lock(_sync)
            {
                _clipboard = new ClipboardData
                {
                    SourcePaths = list,
                    Operation = ClipboardOperationKind.Move,
                    TimestampUtc = DateTime.UtcNow
                };
            }
        }

        public ClipboardData GetData()
        {
            lock(_sync)
            {
                return _clipboard;
            }
        }

        public void Clear()
        {
            lock(_sync)
            {
                _clipboard = new ClipboardData(); // пустой
            }
        }

        /// <summary>
        /// Вставляет (Copy или Move) текущий буфер в указанную директорию.
        /// destinationDirectory - путь каталога назначения (в терминах FileManagerService, т.е. может быть "local://C:/Temp").
        /// Прогресс передается вниз FileManagerService.CopyAsync / MoveAsync.
        /// </summary>
        /// <param name="destinationDirectory"></param>
        /// <param name="progress"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<FileOperationResult>> PasteAsync(string destinationDirectory, IProgressReporter? progress, CancellationToken cancellationToken)
        {
            ClipboardData data;
            lock(_sync)
            {
                data = _clipboard;
            }

            if(data.IsEmpty)
            {
                // Нечего вставлять
                return Array.Empty<FileOperationResult>();
            }

            var results = new List<FileOperationResult>();
            long[] itemSizes = data.SourcePaths.Select(GetLocalItemSize).ToArray();
            long totalBytes = itemSizes.Sum();
            long completedBytes = 0;

            for(int index = 0; index < data.SourcePaths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string srcPath = data.SourcePaths[index];

                // Вычисляем конечный путь назначения:
                // - Берем имя файла/каталога из srcPath
                // - Склеиваем с destinationDirectory
                //
                // ВАЖНО: мы не делаем Path.Combine напрямую, т.к. тут могут быть схемы.
                // FileManagerService.CombinePath не публичный, поэтому мы создадим
                // конечный путь вручную через NormalizePath().
                //
                // Алгоритм:
                //   1. Имя файла/папки = последний сегмент исходного пути.
                //   2. Целевой путь = destinationDirectory + "/" + имя,
                //      но оформленный в терминах схемы назначения.

                string itemName = ExtractNameFromPath(srcPath);

                // destinationDirectory может быть "local://C:/Target"
                // нам нужно получить канонический вид назначения
                //string normalizedDestDir = _fileManager.NormalizePath(destinationDirectory);

                // Сформируем полный путь назначения как `<destDir>/<name>`
                // В local это превратится в "local://C:/Target/filename".
                // В ssh это будет "ssh://user@host:/home/user/filename".
                string finalDestPath = AppendChild(destinationDirectory, itemName);
                IProgressReporter? itemProgress = progress is null
                    ? null
                    : new ClipboardProgressReporter(
                        progress,
                        completedBytes,
                        itemSizes[index],
                        totalBytes,
                        itemName);

                FileOperationResult result;
                if(data.Operation == ClipboardOperationKind.Copy)
                {
                    result = await _fileManager.CopyAsync(
                        srcPath,
                        finalDestPath,
                        itemProgress,
                        cancellationToken);
                } else // Move
                {
                    result = await _fileManager.MoveAsync(
                        srcPath,
                        finalDestPath,
                        itemProgress,
                        cancellationToken);
                }

                results.Add(result);
                if(result.Success)
                    completedBytes += itemSizes[index];
            }

            // Если это был Move и всё прошло успешно для всех элементов — чистим буфер,
            // чтобы не позволить повторно "вставить вырезанное".
            if(data.Operation == ClipboardOperationKind.Move &&
                results.All(r => r.Success))
            {
                Clear();
            }

            return results;
        }

        private static long GetLocalItemSize(string path)
        {
            string nativePath = path.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                ? path[8..]
                : path;

            if(File.Exists(nativePath))
                return new FileInfo(nativePath).Length;
            if(!Directory.Exists(nativePath))
                return 0;

            // Ссылки пропускаются, чтобы оценка совпадала с рекурсивным копированием.
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            return Directory.EnumerateFiles(nativePath, "*", options)
                .Sum(file => new FileInfo(file).Length);
        }

        private sealed class ClipboardProgressReporter: IProgressReporter
        {
            private readonly IProgressReporter _outer;
            private readonly long _completedBytes;
            private readonly long _itemSize;
            private readonly long _totalBytes;
            private readonly string _itemName;

            public ClipboardProgressReporter(
                IProgressReporter outer,
                long completedBytes,
                long itemSize,
                long totalBytes,
                string itemName)
            {
                _outer = outer;
                _completedBytes = completedBytes;
                _itemSize = itemSize;
                _totalBytes = totalBytes;
                _itemName = itemName;
            }

            public void Report(double? value, string? currentInfo = null)
            {
                if(value is null || _totalBytes == 0)
                {
                    _outer.Report(value, currentInfo ?? _itemName);
                    return;
                }

                double overall = (_completedBytes + _itemSize * value.Value) / _totalBytes;
                _outer.Report(Math.Clamp(overall, 0.0, 1.0), currentInfo ?? _itemName);
            }
        }



        // -----------------------
        // Вспомогательные методы
        // -----------------------

        /// <summary>
        /// Возвращает последний сегмент пути:
        /// - "local://C:/Temp/readme.txt" -> "readme.txt"
        /// - "ssh://host:/var/log" -> "log"
        /// - "C:\Temp\readme.txt" -> "readme.txt"
        /// </summary>
        private static string ExtractNameFromPath(string anyPath)
        {
            if(string.IsNullOrWhiteSpace(anyPath))
                return string.Empty;

            // Попробуем сначала отбросить схему
            string native = anyPath;
            int schemeSep = anyPath.IndexOf("://", StringComparison.Ordinal);
            if(schemeSep > 0 && schemeSep + 3 < anyPath.Length)
            {
                native = anyPath.Substring(schemeSep + 3);
            }

            // Теперь просто берем последний сегмент по слэшу/бэкслэшу
            // (файлы могут быть Windows-style или Unix-style)
            var parts = native
                .TrimEnd('/', '\\')
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            if(parts.Length == 0)
                return native; // fallback

            return parts[^1]; // последний элемент
        }

        /// <summary>
        /// Склеивает каталог назначения и имя нового элемента.
        /// Пример:
        ///   destDir="local://C:/Target"
        ///   childName="readme.txt"
        /// → "local://C:/Target/readme.txt"
        ///
        /// Для путей без схемы (например "C:\Target") мы возвращаем "local://C:/Target/readme.txt"
        /// через нормализацию.
        /// </summary>
        private static string AppendChild(string normalizedDestinationDirectory, string childName)
        {
            // normalizedDestinationDirectory гарантированно в виде "<scheme>://<native>"
            int schemeSep = normalizedDestinationDirectory.IndexOf("://", StringComparison.Ordinal);
            if(schemeSep <= 0)
            {
                // крайне маловероятно, но fallback: просто добавим \child
                return normalizedDestinationDirectory.TrimEnd('/', '\\') + Path.DirectorySeparatorChar + childName;
            }

            string scheme = normalizedDestinationDirectory.Substring(0, schemeSep);
            string native = normalizedDestinationDirectory.Substring(schemeSep + 3);

            // Склеиваем native часть аккуратно:
            // - если native заканчивается на \ или /, не добавляем доп. слэш
            // - иначе добавим слэш с учётом платформы?
            //   тут не знаем платформу провайдера => используем "/" как нейтральный разделитель.
            string combinedNative = native.EndsWith("/") || native.EndsWith("\\")
                ? native + childName
                : native + "/" + childName;

            return $"{scheme}://{combinedNative}";
        }
    }
}

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
        /// <exception cref="NotImplementedException"></exception>
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

            foreach(var srcPath in data.SourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

                FileOperationResult result;
                if(data.Operation == ClipboardOperationKind.Copy)
                {
                    result = await _fileManager.CopyAsync(
                        srcPath,
                        finalDestPath,
                        progress,
                        cancellationToken);
                } else // Move
                {
                    result = await _fileManager.MoveAsync(
                        srcPath,
                        finalDestPath,
                        cancellationToken);
                }

                results.Add(result);
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

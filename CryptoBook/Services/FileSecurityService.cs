using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;

namespace CryptoBook.Services
{
    public class FileSecurityService: IFileSecurityService
    {
        private readonly ISecureFileProcessor _secureFileProcessor;

        public FileSecurityService(ISystemItemCreateService createService, ISecureFileProcessor secureFileProcessor)
        {
            ArgumentNullException.ThrowIfNull(createService);
            _secureFileProcessor = secureFileProcessor ?? throw new ArgumentNullException(nameof(secureFileProcessor));
        }

        public Task<FileOperationResult> EncryptAsync( ISystemItem source, string destinationPath, EncryptionTargetMode mode, IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            return ProcessAsync( source, destinationPath, mode, decrypt: false, progress, cancellationToken);
        }

        public Task<FileOperationResult> DecryptAsync( ISystemItem source, string destinationPath, EncryptionTargetMode mode, IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            return ProcessAsync( source, destinationPath, mode, decrypt: true, progress, cancellationToken);
        }

        private async Task<FileOperationResult> ProcessAsync( ISystemItem source, string destinationPath, EncryptionTargetMode mode, bool decrypt, IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(source);

                if(mode == EncryptionTargetMode.Cancels)
                    return FileOperationResult.Fail("Операция отменена.");

                if(mode is not EncryptionTargetMode.SaveAs and not EncryptionTargetMode.ReplaceSource)
                    return FileOperationResult.Fail("Неизвестный режим обработки.");

                if(string.IsNullOrWhiteSpace(destinationPath))
                    return FileOperationResult.Fail("Не указан путь назначения.");

                cancellationToken.ThrowIfCancellationRequested();

                string sourcePath = Path.GetFullPath(source.FullPath);
                string normalizedDestinationPath = Path.GetFullPath(destinationPath);

                IReadOnlyList<FileWorkItem> files;
                IReadOnlyList<string> destinationDirectories = [];

                switch(source)
                {
                    case IFileItem:
                        files = CreateFileWork( sourcePath, normalizedDestinationPath, mode, decrypt);
                        break;

                    case IDirectoryItem:
                    DirectoryWorkPlan directoryPlan = CreateDirectoryWork( sourcePath, normalizedDestinationPath, mode, decrypt, cancellationToken);
                        files = directoryPlan.Files;
                        destinationDirectories = directoryPlan.DestinationDirectories;
                        break;

                    default:
                        throw new NotSupportedException( $"Тип элемента '{source.GetType().Name}' не поддерживается.");
                }

                foreach(string directory in destinationDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(directory);
                }

                long totalBytes = files.Sum(file => file.Size);
                long processedBytes = 0;
                int processedFiles = 0;

                progress?.Report(files.Count == 0 ? 1.0 : 0.0, sourcePath);

                foreach(FileWorkItem file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string? destinationDirectory = Path.GetDirectoryName(file.DestinationPath);
                    if(string.IsNullOrWhiteSpace(destinationDirectory))
                        throw new IOException($"Не удалось определить каталог назначения для '{file.DestinationPath}'.");

                    Directory.CreateDirectory(destinationDirectory);

                    IProgressReporter? fileProgress = progress is null? null:
                    new WeightedProgressReporter( progress, processedBytes, processedFiles, file.Size, totalBytes, files.Count, file.SourcePath);

                    if(decrypt)
                    {
                        await DecryptFileAsync( file, mode, fileProgress, cancellationToken);
                    } else
                    {
                        await _secureFileProcessor.EncryptFileAsync( file.SourcePath, file.DestinationPath, fileProgress, cancellationToken);
                    }

                    processedBytes += file.Size;
                    processedFiles++;
                    progress?.Report( CalculateProgress( processedBytes, processedFiles, totalBytes, files.Count), file.SourcePath);
                }

                progress?.Report(1.0, sourcePath);
                return FileOperationResult.Ok();
            } catch(OperationCanceledException)
            {
                return FileOperationResult.Fail("Операция отменена.");
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        private async Task DecryptFileAsync( FileWorkItem file, EncryptionTargetMode mode, IProgressReporter? progress, CancellationToken cancellationToken)
        {
            if(mode == EncryptionTargetMode.SaveAs)
            {
                await _secureFileProcessor.DecryptFileAsyncToFile( file.SourcePath, file.DestinationPath, progress, cancellationToken);
                return;
            }

            // При расшифровке процессор добавляет сохранённое внутри контейнера
            // расширение к переданному базовому имени. Промежуточный каталог
            // позволяет точно определить получившийся путь и безопасно заменить
            // исходный зашифрованный файл, даже если расширения отличаются.
            string sourceDirectory = Path.GetDirectoryName(file.SourcePath) ?? throw new IOException($"Не удалось определить каталог файла '{file.SourcePath}'.");
            string stagingDirectory = Path.Combine( sourceDirectory, $".cryptobook-{Guid.NewGuid():N}");

            Directory.CreateDirectory(stagingDirectory);

            try
            {
                string stagingBasePath = Path.Combine( stagingDirectory, Path.GetFileName(file.DestinationPath));

                await _secureFileProcessor.DecryptFileAsyncToFile( file.SourcePath, stagingBasePath, progress, cancellationToken);

                string[] stagedFiles = Directory.GetFiles(stagingDirectory);
                if(stagedFiles.Length != 1)
                    throw new IOException("Не удалось определить результат расшифрования.");

                string finalPath = Path.Combine( sourceDirectory, Path.GetFileName(stagedFiles[0]));

                File.Move(stagedFiles[0], finalPath, overwrite: true);

                if(!PathsEqual(finalPath, file.SourcePath) && File.Exists(file.SourcePath))
                    File.Delete(file.SourcePath);
            } finally
            {
                if(Directory.Exists(stagingDirectory))
                    Directory.Delete(stagingDirectory, recursive: true);
            }
        }

        private static IReadOnlyList<FileWorkItem> CreateFileWork(
            string sourcePath,
            string destinationPath,
            EncryptionTargetMode mode,
            bool decrypt)
        {
            FileInfo sourceFile = new(sourcePath);
            if(!sourceFile.Exists)
                throw new FileNotFoundException("Исходный файл не найден.", sourcePath);

            string targetPath = mode == EncryptionTargetMode.ReplaceSource
                ? decrypt
                    ? Path.Combine(
                        sourceFile.DirectoryName
                            ?? throw new IOException($"Не удалось определить каталог файла '{sourcePath}'."),
                        Path.GetFileNameWithoutExtension(sourceFile.Name))
                    : sourceFile.FullName
                : destinationPath;

            return
            [
                new FileWorkItem(
                    sourceFile.FullName,
                    targetPath,
                    sourceFile.Length)
            ];
        }

        private static DirectoryWorkPlan CreateDirectoryWork(
            string sourcePath,
            string destinationPath,
            EncryptionTargetMode mode,
            bool decrypt,
            CancellationToken cancellationToken)
        {
            DirectoryInfo sourceDirectory = new(sourcePath);
            if(!sourceDirectory.Exists)
                throw new DirectoryNotFoundException($"Исходный каталог '{sourcePath}' не найден.");

            string destinationRoot = mode == EncryptionTargetMode.ReplaceSource
                ? sourceDirectory.FullName
                : destinationPath;

            if(mode == EncryptionTargetMode.SaveAs &&
               PathsEqual(sourceDirectory.FullName, destinationRoot))
            {
                throw new IOException(
                    "Для режима «Сохранить как» каталог назначения должен отличаться от исходного.");
            }

            bool destinationIsInsideSource =
                mode == EncryptionTargetMode.SaveAs &&
                IsSameOrDescendantPath(destinationRoot, sourceDirectory.FullName);

            // Материализуем список до создания каталогов назначения. Это также
            // защищает от рекурсивного захвата результатов, если назначение
            // находится внутри исходного дерева.
            FileInfo[] sourceFiles = sourceDirectory.GetFiles(
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    ReturnSpecialDirectories = false
                });

            DirectoryInfo[] sourceDirectories = sourceDirectory.GetDirectories(
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    ReturnSpecialDirectories = false
                });

            List<FileWorkItem> result = new(sourceFiles.Length);

            foreach(FileInfo sourceFile in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(destinationIsInsideSource &&
                   IsSameOrDescendantPath(sourceFile.FullName, destinationRoot))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(
                    sourceDirectory.FullName,
                    sourceFile.FullName);
                string relativeTargetPath = decrypt
                    ? Path.ChangeExtension(relativePath, null)
                    : relativePath;
                string targetPath = Path.Combine(destinationRoot, relativeTargetPath);

                result.Add(new FileWorkItem(
                    sourceFile.FullName,
                    targetPath,
                    sourceFile.Length));
            }

            List<string> directories = [];
            if(mode == EncryptionTargetMode.SaveAs)
            {
                directories.Add(destinationRoot);

                foreach(DirectoryInfo directory in sourceDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if(destinationIsInsideSource &&
                       IsSameOrDescendantPath(directory.FullName, destinationRoot))
                    {
                        continue;
                    }

                    directories.Add(Path.Combine(
                        destinationRoot,
                        Path.GetRelativePath(
                            sourceDirectory.FullName,
                            directory.FullName)));
                }
            }

            return new DirectoryWorkPlan(result, directories);
        }

        private static double CalculateProgress(
            long processedBytes,
            int processedFiles,
            long totalBytes,
            int fileCount)
        {
            if(totalBytes > 0)
                return Math.Clamp((double)processedBytes / totalBytes, 0.0, 1.0);

            return fileCount == 0
                ? 1.0
                : Math.Clamp((double)processedFiles / fileCount, 0.0, 1.0);
        }

        private static bool PathsEqual(string firstPath, string secondPath)
        {
            return string.Equals(
                Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameOrDescendantPath(
            string candidatePath,
            string parentPath)
        {
            string candidate = Path.GetFullPath(candidatePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parent = Path.GetFullPath(parentPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return candidate.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
                   candidate.StartsWith(
                       parent + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        private sealed record FileWorkItem(
            string SourcePath,
            string DestinationPath,
            long Size);

        private sealed record DirectoryWorkPlan(
            IReadOnlyList<FileWorkItem> Files,
            IReadOnlyList<string> DestinationDirectories);

        private sealed class WeightedProgressReporter: IProgressReporter
        {
            private readonly IProgressReporter _outerProgress;
            private readonly long _processedBytes;
            private readonly int _processedFiles;
            private readonly long _fileSize;
            private readonly long _totalBytes;
            private readonly int _totalFiles;
            private readonly string _currentFile;

            public WeightedProgressReporter(
                IProgressReporter outerProgress,
                long processedBytes,
                int processedFiles,
                long fileSize,
                long totalBytes,
                int totalFiles,
                string currentFile)
            {
                _outerProgress = outerProgress;
                _processedBytes = processedBytes;
                _processedFiles = processedFiles;
                _fileSize = fileSize;
                _totalBytes = totalBytes;
                _totalFiles = totalFiles;
                _currentFile = currentFile;
            }

            public void Report(double? value, string? currentInfo = null)
            {
                if(value is null)
                {
                    _outerProgress.Report(null, currentInfo ?? _currentFile);
                    return;
                }

                double normalizedFileProgress = Math.Clamp(value.Value, 0.0, 1.0);
                double overallProgress = _totalBytes > 0
                    ? (_processedBytes + _fileSize * normalizedFileProgress) / _totalBytes
                    : (_processedFiles + normalizedFileProgress) / _totalFiles;

                _outerProgress.Report(
                    Math.Clamp(overallProgress, 0.0, 1.0),
                    currentInfo ?? _currentFile);
            }
        }
    }
}

using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using CryptoBook.Infrastructure;

using System.IO;

namespace CryptoBook.Services
{
    public class FileSecurityService: IFileSecurityService
    {
        private readonly ISecureFileProcessor _secureFileProcessor;
        private readonly IKeyResetService? keyResetService;

        public FileSecurityService(
            ISystemItemCreateService createService,
            ISecureFileProcessor secureFileProcessor,
            IKeyResetService? keyResetService = null)
        {
            ArgumentNullException.ThrowIfNull(createService);
            _secureFileProcessor = secureFileProcessor ?? throw new ArgumentNullException(nameof(secureFileProcessor));
            this.keyResetService = keyResetService;
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

        public Task<FileOperationBatchResult> EncryptAsync(
            IReadOnlyList<ISystemItem> sources,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            return ProcessBatchAsync(
                sources,
                decrypt: false,
                progress,
                cancellationToken);
        }

        public Task<FileOperationBatchResult> DecryptAsync(
            IReadOnlyList<ISystemItem> sources,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            return ProcessBatchAsync(
                sources,
                decrypt: true,
                progress,
                cancellationToken);
        }

        private async Task<FileOperationBatchResult> ProcessBatchAsync(
            IReadOnlyList<ISystemItem> sources,
            bool decrypt,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(sources);

            if(sources.Count == 0)
                return new FileOperationBatchResult([], 0, 0, false, false);

            var work = new List<BatchSourceWork>(sources.Count);
            foreach(ISystemItem source in sources)
            {
                try
                {
                    work.Add(MeasureSourceWork(source, cancellationToken));
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch(Exception ex)
                {
                    work.Add(new BatchSourceWork(
                        source,
                        1,
                        FormatPathError(source.FullPath, ex.Message)));
                }
            }

            long totalWorkUnits = work.Aggregate(
                0L,
                (total, item) => SaturatingAdd(total, item.WorkUnits));
            long completedWorkUnits = 0;
            int completedCount = 0;
            bool hasFailures = false;
            var results = new List<FileOperationResult>(work.Count);

            progress?.Report(0.0, work[0].Source.FullPath);

            foreach(BatchSourceWork item in work)
            {
                var itemProgress = progress is null
                    ? null
                    : new BatchSourceProgressReporter(
                        progress,
                        completedWorkUnits,
                        item.WorkUnits,
                        totalWorkUnits,
                        item.Source.FullPath);

                FileOperationResult result;
                if(item.PreparationError is not null)
                {
                    result = FileOperationResult.Fail(item.PreparationError);
                }
                else
                {
                    try
                    {
                        result = await ProcessAsync(
                            item.Source,
                            item.Source.FullPath,
                            EncryptionTargetMode.ReplaceSource,
                        decrypt,
                        itemProgress,
                        cancellationToken,
                        continueAfterFileError: true);
                    }
                    catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                    {
                        results.Add(new FileOperationResult
                        {
                            Success = false,
                            ErrorMessage = LocalizationManager.GetString(
                                "Error.OperationCanceled"),
                            AffectedPath = item.Source.FullPath
                        });
                        return new FileOperationBatchResult(
                            results,
                            completedCount,
                            0,
                            true,
                            true);
                    }
                }

                results.Add(new FileOperationResult
                {
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage,
                    AffectedPath = item.Source.FullPath
                });

                if(!result.Success)
                    hasFailures = true;
                else
                    completedCount++;

                completedWorkUnits = SaturatingAdd(
                    completedWorkUnits,
                    item.WorkUnits);
                progress?.Report(
                    CalculateBatchProgress(completedWorkUnits, totalWorkUnits),
                    item.Source.FullPath);
            }

            progress?.Report(1.0, work[^1].Source.FullPath);
            return new FileOperationBatchResult(
                results,
                completedCount,
                0,
                false,
                hasFailures);
        }

        private async Task<FileOperationResult> ProcessAsync( ISystemItem source, string destinationPath, EncryptionTargetMode mode, bool decrypt, IProgressReporter? progress,
            CancellationToken cancellationToken, bool continueAfterFileError = false)
        {
            if(keyResetService?.State is KeyResetState.Resetting or KeyResetState.Restoring)
                return FileOperationResult.Fail("Выполняется безопасный сброс ключа.");
            using IDisposable? timerPause = keyResetService?.Pause();
            string? currentPath = null;
            try
            {
                ArgumentNullException.ThrowIfNull(source);

                if(mode == EncryptionTargetMode.Cancels)
                    return FileOperationResult.Fail(
                        LocalizationManager.GetString("Error.OperationCanceled"));

                if(mode is not EncryptionTargetMode.SaveAs and not EncryptionTargetMode.ReplaceSource)
                    return FileOperationResult.Fail(
                        LocalizationManager.GetString("Security.UnknownMode"));

                if(string.IsNullOrWhiteSpace(destinationPath))
                    return FileOperationResult.Fail(
                        LocalizationManager.GetString(
                            "Security.DestinationRequired"));

                cancellationToken.ThrowIfCancellationRequested();

                string sourcePath = Path.GetFullPath(source.FullPath);
                currentPath = sourcePath;
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
                        throw new NotSupportedException(
                            LocalizationManager.Format(
                                "Security.UnsupportedItemType",
                                source.GetType().Name));
                }

                foreach(string directory in destinationDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(directory);
                }

                long totalBytes = files.Sum(file => file.Size);
                long processedBytes = 0;
                int processedFiles = 0;
                var fileErrors = new List<string>();

                progress?.Report(files.Count == 0 ? 1.0 : 0.0, sourcePath);

                foreach(FileWorkItem file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    currentPath = file.SourcePath;

                    try
                    {
                        string? destinationDirectory = Path.GetDirectoryName(file.DestinationPath);
                        if(string.IsNullOrWhiteSpace(destinationDirectory))
                            throw new IOException(
                                LocalizationManager.Format(
                                    "Security.DestinationDirectoryUnknown",
                                    file.DestinationPath));

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
                    }
                    catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch(Exception ex) when(continueAfterFileError)
                    {
                        fileErrors.Add(FormatPathError(file.SourcePath, ex.Message));
                    }

                    processedBytes += file.Size;
                    processedFiles++;
                    progress?.Report( CalculateProgress( processedBytes, processedFiles, totalBytes, files.Count), file.SourcePath);
                }

                progress?.Report(1.0, sourcePath);
                if(fileErrors.Count > 0)
                {
                    return FileOperationResult.Fail(string.Join(
                        Environment.NewLine + Environment.NewLine,
                        fileErrors));
                }

                return FileOperationResult.Ok();
            } catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                throw;
            } catch(OperationCanceledException)
            {
                return FileOperationResult.Fail(
                    LocalizationManager.GetString("Error.OperationCanceled"));
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(
                    string.IsNullOrWhiteSpace(currentPath)
                        ? ex.Message
                        : FormatPathError(currentPath, ex.Message));
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
            string sourceDirectory = Path.GetDirectoryName(file.SourcePath) ??
                throw new IOException(
                    LocalizationManager.Format(
                        "Security.SourceDirectoryUnknown",
                        file.SourcePath));
            string stagingDirectory = Path.Combine( sourceDirectory, $".cryptobook-{Guid.NewGuid():N}");

            Directory.CreateDirectory(stagingDirectory);

            try
            {
                string stagingBasePath = Path.Combine( stagingDirectory, Path.GetFileName(file.DestinationPath));

                await _secureFileProcessor.DecryptFileAsyncToFile( file.SourcePath, stagingBasePath, progress, cancellationToken);

                string[] stagedFiles = Directory.GetFiles(stagingDirectory);
                if(stagedFiles.Length != 1)
                    throw new IOException(
                        LocalizationManager.GetString(
                            "Security.DecryptionResultUnknown"));

                string finalPath = Path.Combine( sourceDirectory, Path.GetFileName(stagedFiles[0]));

                AtomicFileCommit.CommitWithoutBackup(stagedFiles[0], finalPath);

                if(!PathsEqual(finalPath, file.SourcePath) && File.Exists(file.SourcePath))
                    AtomicFileCommit.DeleteIfExists(file.SourcePath);
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
                throw new FileNotFoundException(
                    LocalizationManager.GetString(
                        "Security.SourceFileNotFound"),
                    sourcePath);

            string targetPath = mode == EncryptionTargetMode.ReplaceSource
                ? decrypt
                    ? Path.Combine(
                        sourceFile.DirectoryName
                            ?? throw new IOException(
                                LocalizationManager.Format(
                                    "Security.SourceDirectoryUnknown",
                                    sourcePath)),
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
                throw new DirectoryNotFoundException(
                    LocalizationManager.Format(
                        "Security.SourceDirectoryNotFound",
                        sourcePath));

            string destinationRoot = mode == EncryptionTargetMode.ReplaceSource
                ? sourceDirectory.FullName
                : destinationPath;

            if(mode == EncryptionTargetMode.SaveAs &&
               PathsEqual(sourceDirectory.FullName, destinationRoot))
            {
                throw new IOException(
                    LocalizationManager.GetString(
                        "Security.SaveAsDirectoryMustDiffer"));
            }

            bool destinationIsInsideSource =
                mode == EncryptionTargetMode.SaveAs &&
                IsSameOrDescendantPath(destinationRoot, sourceDirectory.FullName);

            // Материализуем список до создания каталогов назначения. Это также
            // защищает от рекурсивного захвата результатов, если назначение
            // находится внутри исходного дерева.
            FileInfo[] sourceFiles = sourceDirectory.GetFiles(
                "*",
                CreateRecursiveEnumerationOptions());

            DirectoryInfo[] sourceDirectories = sourceDirectory.GetDirectories(
                "*",
                CreateRecursiveEnumerationOptions());

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

        private static BatchSourceWork MeasureSourceWork(
            ISystemItem source,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();

            switch(source)
            {
                case IFileItem:
                    FileInfo file = new(source.FullPath);
                    if(!file.Exists)
                        throw new FileNotFoundException(
                            LocalizationManager.GetString(
                                "Security.SourceFileNotFound"),
                            source.FullPath);
                    return new BatchSourceWork(
                        source,
                        Math.Max(1L, file.Length));

                case IDirectoryItem:
                    DirectoryInfo directory = new(source.FullPath);
                    if(!directory.Exists)
                        throw new DirectoryNotFoundException(
                            LocalizationManager.Format(
                                "Security.SourceDirectoryNotFound",
                                source.FullPath));

                    long totalBytes = 0;
                    long fileCount = 0;
                    foreach(FileInfo child in directory.EnumerateFiles(
                        "*",
                        CreateRecursiveEnumerationOptions()))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        totalBytes = SaturatingAdd(totalBytes, child.Length);
                        fileCount = SaturatingAdd(fileCount, 1);
                    }

                    return new BatchSourceWork(
                        source,
                        totalBytes > 0
                            ? totalBytes
                            : Math.Max(1L, fileCount));

                default:
                    throw new NotSupportedException(
                        LocalizationManager.Format(
                            "Security.UnsupportedItemType",
                            source.GetType().Name));
            }
        }

        private static EnumerationOptions CreateRecursiveEnumerationOptions() =>
            new()
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint,
                ReturnSpecialDirectories = false
            };

        private static long SaturatingAdd(long left, long right) =>
            left > long.MaxValue - right ? long.MaxValue : left + right;

        private static double CalculateBatchProgress(
            long completedWorkUnits,
            long totalWorkUnits) =>
            totalWorkUnits <= 0
                ? 1.0
                : Math.Clamp(
                    (double)completedWorkUnits / totalWorkUnits,
                    0.0,
                    1.0);

        private static string FormatPathError(string path, string message) =>
            LocalizationManager.Format(
                "Security.OperationFailedForPath",
                Environment.NewLine,
                path,
                message);

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

        private sealed record BatchSourceWork(
            ISystemItem Source,
            long WorkUnits,
            string? PreparationError = null);

        private sealed class BatchSourceProgressReporter: IProgressReporter
        {
            private readonly IProgressReporter outerProgress;
            private readonly long completedWorkUnits;
            private readonly long currentWorkUnits;
            private readonly long totalWorkUnits;
            private readonly string currentPath;

            public BatchSourceProgressReporter(
                IProgressReporter outerProgress,
                long completedWorkUnits,
                long currentWorkUnits,
                long totalWorkUnits,
                string currentPath)
            {
                this.outerProgress = outerProgress;
                this.completedWorkUnits = completedWorkUnits;
                this.currentWorkUnits = currentWorkUnits;
                this.totalWorkUnits = totalWorkUnits;
                this.currentPath = currentPath;
            }

            public void Report(double? value, string? currentInfo = null)
            {
                if(value is null)
                {
                    outerProgress.Report(null, currentInfo ?? currentPath);
                    return;
                }

                double currentProgress = Math.Clamp(value.Value, 0.0, 1.0);
                double aggregate = totalWorkUnits <= 0
                    ? 1.0
                    : (completedWorkUnits + currentWorkUnits * currentProgress) /
                        totalWorkUnits;
                outerProgress.Report(
                    Math.Clamp(aggregate, 0.0, 1.0),
                    currentInfo ?? currentPath);
            }
        }

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

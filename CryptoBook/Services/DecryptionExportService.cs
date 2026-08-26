using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.Diagnostics;
using System.IO;

namespace CryptoBook.Services
{
    public sealed class DecryptionExportService: IDecryptionExportService
    {
        private readonly ISecureFileProcessor secureFileProcessor;
        private readonly ISecureFileValidator secureFileValidator;
        private readonly IDecryptedDocumentConversionService conversionService;
        private readonly string temporaryRoot;

        public DecryptionExportService(
            ISecureFileProcessor secureFileProcessor,
            ISecureFileValidator secureFileValidator,
            IDecryptedDocumentConversionService conversionService)
            : this(
                secureFileProcessor,
                secureFileValidator,
                conversionService,
                GetDefaultTemporaryRoot())
        {
        }

        public DecryptionExportService(
            ISecureFileProcessor secureFileProcessor,
            ISecureFileValidator secureFileValidator,
            IDecryptedDocumentConversionService conversionService,
            string temporaryRoot)
        {
            this.secureFileProcessor = secureFileProcessor ??
                throw new ArgumentNullException(nameof(secureFileProcessor));
            this.secureFileValidator = secureFileValidator ??
                throw new ArgumentNullException(nameof(secureFileValidator));
            this.conversionService = conversionService ??
                throw new ArgumentNullException(nameof(conversionService));
            this.temporaryRoot = Path.GetFullPath(temporaryRoot);
            CleanupOrphanedDirectories(this.temporaryRoot);
        }

        public async Task<PreparedDecryption> PrepareAsync(
            string sourcePath,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            string fullSourcePath = Path.GetFullPath(sourcePath);
            if(!await secureFileValidator.HasCryptoBookHeaderAsync(
                fullSourcePath,
                cancellationToken))
            {
                throw new InvalidDataException(
                    LocalizationManager.GetString(
                        "Security.NotEncryptedCryptoBookFile"));
            }

            string directory = Path.Combine(
                temporaryRoot,
                $"{Environment.ProcessId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string contentPath = Path.Combine(directory, "payload.bin");

            try
            {
                await using DecryptedFileContent decrypted =
                    await secureFileProcessor.DecryptFileContentAsync(
                        fullSourcePath,
                        progress,
                        cancellationToken);
                await using(FileStream output = new(
                    contentPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await decrypted.Content.CopyToAsync(
                        output,
                        cancellationToken);
                    await output.FlushAsync(cancellationToken);
                    output.Flush(flushToDisk: true);
                }

                return new PreparedDecryption(
                    fullSourcePath,
                    contentPath,
                    NormalizeExtension(decrypted.OriginalExtension),
                    directory);
            }
            catch
            {
                TryDeleteDirectory(directory);
                throw;
            }
        }

        public IReadOnlyList<DecryptionOutputFormat> GetAvailableFormats(
            string originalExtension) =>
            conversionService.CanConvert(originalExtension)
                ? [
                    DecryptionOutputFormat.Rtf,
                    DecryptionOutputFormat.PlainText,
                    DecryptionOutputFormat.Original
                  ]
                : [DecryptionOutputFormat.Original];

        public DecryptionOutputFormat GetDefaultFormat(
            string originalExtension) =>
            NormalizeExtension(originalExtension).Equals(
                ".XamlPackage",
                StringComparison.OrdinalIgnoreCase) &&
            conversionService.CanConvert(originalExtension)
                ? DecryptionOutputFormat.Rtf
                : DecryptionOutputFormat.Original;

        public string GetOutputExtension(
            string originalExtension,
            DecryptionOutputFormat outputFormat) => outputFormat switch
            {
                DecryptionOutputFormat.Original =>
                    NormalizeExtension(originalExtension),
                DecryptionOutputFormat.Rtf => ".rtf",
                DecryptionOutputFormat.PlainText => ".txt",
                _ => throw new ArgumentOutOfRangeException(nameof(outputFormat))
            };

        public async Task<string> PublishAsync(
            PreparedDecryption prepared,
            DecryptionOptions options,
            string destinationPath,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
            if(options.TargetMode is not EncryptionTargetMode.SaveAs and
                not EncryptionTargetMode.ReplaceSource)
            {
                throw new ArgumentException(
                    LocalizationManager.GetString("Security.UnknownMode"),
                    nameof(options));
            }
            if(!GetAvailableFormats(prepared.OriginalExtension)
                .Contains(options.OutputFormat))
            {
                throw new NotSupportedException(
                    LocalizationManager.GetString(
                        "DecryptionExport.UnsupportedConversion"));
            }

            string finalPath = Path.ChangeExtension(
                Path.GetFullPath(destinationPath),
                GetOutputExtension(
                    prepared.OriginalExtension,
                    options.OutputFormat));
            if(File.Exists(finalPath) &&
               !PathsEqual(finalPath, prepared.SourcePath))
            {
                finalPath = GetUniqueFilePath(finalPath);
            }

            string? destinationDirectory = Path.GetDirectoryName(finalPath);
            if(string.IsNullOrWhiteSpace(destinationDirectory))
                throw new IOException(
                    LocalizationManager.Format(
                        "Security.DestinationDirectoryUnknown",
                        finalPath));
            Directory.CreateDirectory(destinationDirectory);
            string stagingPath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await using FileStream source = new(
                    prepared.ContentPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using(FileStream destination = new(
                    stagingPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    if(options.OutputFormat == DecryptionOutputFormat.Original)
                    {
                        await CopyOriginalAsync(
                            source,
                            destination,
                            finalPath,
                            progress,
                            cancellationToken);
                    }
                    else
                    {
                        await conversionService.ConvertAsync(
                            source,
                            prepared.OriginalExtension,
                            options.OutputFormat,
                            destination,
                            cancellationToken);
                    }
                    await destination.FlushAsync(cancellationToken);
                    destination.Flush(flushToDisk: true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(stagingPath, finalPath);
                progress?.Report(1.0, finalPath);

                if(options.TargetMode == EncryptionTargetMode.ReplaceSource &&
                   !PathsEqual(finalPath, prepared.SourcePath) &&
                   File.Exists(prepared.SourcePath))
                {
                    AtomicFileCommit.DeleteIfExists(prepared.SourcePath);
                }

                return finalPath;
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                throw new IOException(
                    LocalizationManager.Format(
                        "DecryptionExport.ConversionFailed",
                        ex.Message),
                    ex);
            }
            finally
            {
                TryDeleteFile(stagingPath);
            }
        }

        private static async Task CopyOriginalAsync(
            Stream source,
            Stream destination,
            string finalPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[81920];
            long copied = 0;
            int read;
            while((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
                copied += read;
                progress?.Report(
                    source.Length == 0 ? 1.0 : (double)copied / source.Length,
                    finalPath);
            }
        }

        private static string NormalizeExtension(string extension)
        {
            if(string.IsNullOrWhiteSpace(extension))
                throw new ArgumentException(
                    "Original extension is required.",
                    nameof(extension));
            return extension.StartsWith('.') ? extension : $".{extension}";
        }

        private static string GetUniqueFilePath(string desiredPath)
        {
            string directory = Path.GetDirectoryName(desiredPath) ??
                throw new IOException(
                    LocalizationManager.Format(
                        "Security.DestinationDirectoryUnknown",
                        desiredPath));
            string name = Path.GetFileNameWithoutExtension(desiredPath);
            string extension = Path.GetExtension(desiredPath);
            for(int index = 2; ; index++)
            {
                string candidate = Path.Combine(
                    directory,
                    $"{name} ({index}){extension}");
                if(!File.Exists(candidate))
                    return candidate;
            }
        }

        private static string GetDefaultTemporaryRoot() =>
            Path.Combine(Path.GetTempPath(), "CryptoBook", "Decrypt");

        private static void CleanupOrphanedDirectories(string root)
        {
            if(!Directory.Exists(root))
                return;
            foreach(string directory in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(directory);
                int separator = name.IndexOf('-');
                if(separator <= 0 ||
                   !int.TryParse(name.AsSpan(0, separator), out int processId) ||
                   IsProcessRunning(processId))
                {
                    continue;
                }
                TryDeleteDirectory(directory);
            }
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch(ArgumentException)
            {
                return false;
            }
        }

        private static bool PathsEqual(string first, string second) =>
            string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase);

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if(Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch(IOException)
            {
            }
            catch(UnauthorizedAccessException)
            {
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if(File.Exists(path))
                    File.Delete(path);
            }
            catch(IOException)
            {
            }
            catch(UnauthorizedAccessException)
            {
            }
        }
    }
}

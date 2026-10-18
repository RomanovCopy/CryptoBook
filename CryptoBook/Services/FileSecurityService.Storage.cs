using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;
using System.Security.Cryptography;

namespace CryptoBook.Services;

public partial class FileSecurityService
{
    private readonly IStorageFacade storage;
    private readonly ITransferEngine transferEngine;

    private async Task<FileOperationBatchResult> CreateStorageProtectedCopiesAsync(
        IReadOnlyList<ISystemItem> sources,
        string destinationDirectory,
        IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        using IDisposable? timerPause = keyResetService?.Pause();
        var results = new List<FileOperationResult>();
        int completed = 0;
        int skipped = 0;
        bool failed = false;
        StorageLocation destination = storage.Resolve(destinationDirectory);
        foreach(ISystemItem source in sources)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                StorageItemMetadata metadata = await storage.GetMetadataAsync(
                    source.Location, cancellationToken) ?? throw new FileNotFoundException(
                        LocalizationManager.GetString("Security.SourceFileNotFound"));
                string target = storage.Format(storage.GetChild(
                    destination,
                    Path.GetFileNameWithoutExtension(metadata.Name) + ".cbook"));
                FileOperationResult result = await ProcessStorageFileAsync(
                    source, target, EncryptionTargetMode.SaveAs, decrypt: false,
                    progress is null ? null : new BatchSourceProgressReporter(
                        progress, results.Count, 1, sources.Count, source.DisplayPath),
                    cancellationToken);
                results.Add(result);
                completed += result.ProcessedFileCount;
                skipped += result.SkippedFileCount;
                failed |= !result.Success;
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                return new FileOperationBatchResult(
                    results, completed, skipped, true, completed > 0);
            }
            catch(Exception ex)
            {
                results.Add(FileOperationResult.Fail(FormatPathError(source.DisplayPath, ex.Message)));
                failed = true;
            }
        }
        return new FileOperationBatchResult(
            results, completed, skipped, false, failed && completed > 0);
    }

    private async Task<FileOperationResult> ProcessStorageFileAsync(
        ISystemItem source,
        string destinationPath,
        EncryptionTargetMode mode,
        bool decrypt,
        IProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        string workspace = Path.Combine(
            Path.GetTempPath(), "CryptoBook", "FileSecurity", Guid.NewGuid().ToString("N"));
        try
        {
            if(keyResetService?.State is KeyResetState.Resetting or KeyResetState.Restoring or
                KeyResetState.Unlocking || (keyResetService?.State == KeyResetState.KeyReset &&
                keyResetService.HasRetainedDocument))
                return FileOperationResult.Fail("Выполняется безопасный сброс ключа.");
            using IDisposable? timerPause = keyResetService?.Pause();
            if(source is not IFileItem)
                throw new NotSupportedException(LocalizationManager.Format(
                    "Security.UnsupportedItemType", source.GetType().Name));
            if(!decrypt && keyRequestService?.EnsureEncryptionKeyAvailable() == false)
                throw new OperationCanceledException(cancellationToken);

            StorageItemMetadata metadata = await storage.GetMetadataAsync(
                source.Location, cancellationToken) ?? throw new FileNotFoundException(
                    LocalizationManager.GetString("Security.SourceFileNotFound"));
            Directory.CreateDirectory(workspace);
            // Use only the provider's file name for the extension; never treat
            // an opaque Android/MTP locator as a Windows path.
            string localSource = Path.Combine(workspace, "source" + Path.GetExtension(metadata.Name));
            FileOperationResult download = await transferEngine.CopyAsync(
                source.Location, storage.Resolve(localSource), StageProgress(0, 1), cancellationToken);
            if(!download.Success)
                throw new IOException(download.ErrorMessage);
            await EnsureSourceUnchangedAsync(source.Location, localSource, cancellationToken);

            bool encrypted = await secureFileValidator.HasCryptoBookHeaderAsync(
                localSource, cancellationToken);
            if(encrypted != decrypt)
                return FileOperationResult.Ok(source.FullPath, skippedFileCount: 1);

            string output = Path.Combine(workspace, "result.cbook");
            var localItem = new FileItem
            {
                FullPath = localSource,
                Name = Path.GetFileName(localSource),
                Extension = Path.GetExtension(localSource)
            };
            FileOperationResult processed = await ProcessAsync(
                localItem, output, EncryptionTargetMode.SaveAs, decrypt,
                StageProgress(1, 3), cancellationToken);
            if(!processed.Success)
                return processed;
            output = processed.AffectedPath ?? output;
            if(!decrypt)
            {
                FileOperationResult verified = await VerifyProtectedCopyAsync(
                    localSource, output, cancellationToken);
                if(!verified.Success)
                    return verified;
            }

            StorageLocation destination = storage.Resolve(destinationPath);
            string outputExtension = decrypt ? Path.GetExtension(output) : ".cbook";
            if(mode == EncryptionTargetMode.ReplaceSource)
            {
                StorageLocation parent = storage.GetParent(source.Location) ??
                    throw new IOException(LocalizationManager.Format(
                        "Security.SourceDirectoryUnknown", source.DisplayPath));
                destination = storage.GetChild(
                    parent, Path.GetFileNameWithoutExtension(metadata.Name) + outputExtension);
            }
            else if(decrypt)
            {
                StorageItemMetadata? target = await storage.GetMetadataAsync(
                    destination, cancellationToken);
                StorageLocation parent = storage.GetParent(destination) ??
                    throw new IOException(LocalizationManager.Format(
                        "Security.DestinationDirectoryUnknown", destinationPath));
                string name = target?.Name ?? GetStorageName(destination);
                destination = storage.GetChild(parent, Path.ChangeExtension(name, outputExtension));
            }
            destination = await GetFreeStorageFileAsync(destination, cancellationToken);

            // Cross-provider moves verify size and SHA256 before deleting the
            // local result. The user's source survives any failed upload.
            FileOperationResult upload = await transferEngine.MoveAsync(
                storage.Resolve(output), destination, StageProgress(4, 1), cancellationToken);
            if(!upload.Success)
                throw new IOException(upload.ErrorMessage);
            if(mode == EncryptionTargetMode.ReplaceSource)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await EnsureSourceUnchangedAsync(source.Location, localSource, cancellationToken);
                FileOperationResult deleted = await storage.GetProvider(source.Location)
                    .DeleteAsync(source.Location, cancellationToken);
                if(!deleted.Success)
                    throw new IOException(deleted.ErrorMessage);
            }
            return FileOperationResult.Ok(storage.Format(destination), processedFileCount: 1);

            IProgressReporter? StageProgress(long completed, long weight) => progress is null
                ? null : new BatchSourceProgressReporter(
                    progress, completed, weight, 5, source.DisplayPath);
        }
        catch(OperationCanceledException)
        {
            throw;
        }
        catch(Exception ex)
        {
            return FileOperationResult.Fail(FormatPathError(source.DisplayPath, ex.Message));
        }
        finally
        {
            try
            {
                if(Directory.Exists(workspace))
                    Directory.Delete(workspace, recursive: true);
            }
            catch { }
        }
    }

    private async Task EnsureSourceUnchangedAsync(
        StorageLocation source, string localCopy, CancellationToken cancellationToken)
    {
        await using Stream original = await storage.GetProvider(source)
            .OpenRawReadAsync(source, cancellationToken);
        await using Stream copy = File.OpenRead(localCopy);
        byte[] originalHash = await SHA256.HashDataAsync(original, cancellationToken);
        byte[] copyHash = await SHA256.HashDataAsync(copy, cancellationToken);
        if(!originalHash.SequenceEqual(copyHash))
            throw new IOException(LocalizationManager.GetString("Security.ProtectedCopyVerificationFailed"));
    }

    private string GetStorageName(StorageLocation location)
    {
        // Resolve names through the provider's display path, never the locator.
        string displayPath = storage.FormatDisplayPath(location);
        return Path.GetFileName(displayPath);
    }

    private async Task<StorageLocation> GetFreeStorageFileAsync(
        StorageLocation desired,
        CancellationToken cancellationToken)
    {
        if(await storage.GetMetadataAsync(desired, cancellationToken) is null)
            return desired;
        StorageLocation parent = storage.GetParent(desired) ??
            throw new IOException(LocalizationManager.Format(
                "Security.DestinationDirectoryUnknown", storage.FormatDisplayPath(desired)));
        string name = GetStorageName(desired);
        for(int index = 2; ; index++)
        {
            StorageLocation candidate = storage.GetChild(parent,
                $"{Path.GetFileNameWithoutExtension(name)} ({index}){Path.GetExtension(name)}");
            if(await storage.GetMetadataAsync(candidate, cancellationToken) is null)
                return candidate;
        }
    }
}

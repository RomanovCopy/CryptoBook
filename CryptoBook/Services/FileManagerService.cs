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
    /// Фасад над всеми файловыми провайдерами + общая логика ошибок.
    /// ViewModel и команды должны общаться только с этим сервисом.
    /// </summary>
    public sealed class FileManagerService: IFileManagerService
    {
        private readonly IReadOnlyDictionary<string, IFileProviderService> _providersByScheme;
        private readonly IStorageFacade _storage;
        private readonly ITransferEngine _transferEngine;


        /// <summary>
        /// В конструктор передаем все зарегистрированные IFileSystemProvider.
        /// Autofac сам вольет сюда все реализации (LocalFileSystemProvider и будущие Zip/Ssh/...).
        /// </summary>
        public FileManagerService(
            IEnumerable<IFileProviderService> providers,
            IStorageFacade? storage = null,
            ITransferEngine? transferEngine = null)
        {
            // Если два провайдера объявят одинаковую схему — это повод упасть сразу, а не в рантайме.
            _providersByScheme = providers
                .GroupBy(p => p.Scheme, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Single(),
                    StringComparer.OrdinalIgnoreCase
                );
            _storage = storage ?? new StorageFacade([new LocalStorageProvider()]);
            _transferEngine = transferEngine ?? new TransferEngine(_storage);
        }


        public Task<List<ISystemItem>> BrowseAsync(string path, IProgressReporter? progress = null, CancellationToken ct = default, bool includeHidden = false)
        {
            var desc = ParsePath(path); // как раньше                                           
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return provider.GetContainerContentAsync(desc.NativePath, progress, ct, includeHidden);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                throw new IOException($"Failed to browse path '{path}': {ex.Message}", ex);
            }

        }

        public async Task<FileOperationResult> CopyAsync(string sourcePath, string destinationPath, IProgressReporter? progress, CancellationToken cancellationToken = default)
        {
            var src = ParsePath(sourcePath);
            var dst = ParsePath(destinationPath);

            if(!string.Equals(src.Scheme, dst.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return await _transferEngine.CopyAsync(
                    _storage.Resolve(sourcePath),
                    _storage.Resolve(destinationPath),
                    progress,
                    cancellationToken);
            }

            if(!src.Scheme.Equals(StorageLocation.LocalProviderId, StringComparison.OrdinalIgnoreCase))
            {
                return await _transferEngine.CopyAsync(
                    _storage.Resolve(sourcePath),
                    _storage.Resolve(destinationPath),
                    progress,
                    cancellationToken);
            }

            StorageLocation sourceLocation = _storage.Resolve(sourcePath);
            StorageLocation destinationLocation = _storage.Resolve(destinationPath);
            if(_storage.AreEquivalent(sourceLocation, destinationLocation) ||
               _storage.IsDescendant(sourceLocation, destinationLocation))
            {
                throw new InvalidOperationException( "Нельзя копировать каталог в самого себя " + "или во вложенный подкаталог.");
            }

            var provider = ResolveProvider(src.Scheme);
            try
            {
                return await provider.CopyAsync(src.NativePath, dst.NativePath, progress, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return FileOperationResult.Fail($"Copy failed: {ex.Message}");
            }
        }

        public async Task<FileOperationResult> MoveAsync(string sourcePath, string destinationPath, IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            var src = ParsePath(sourcePath);
            var dst = ParsePath(destinationPath);

            if(!string.Equals(src.Scheme, dst.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return await _transferEngine.MoveAsync(
                    _storage.Resolve(sourcePath),
                    _storage.Resolve(destinationPath),
                    progress,
                    cancellationToken);
            }


            if(!src.Scheme.Equals(StorageLocation.LocalProviderId, StringComparison.OrdinalIgnoreCase))
            {
                return await _transferEngine.MoveAsync(
                    _storage.Resolve(sourcePath),
                    _storage.Resolve(destinationPath),
                    progress,
                    cancellationToken);
            }

            StorageLocation sourceLocation = _storage.Resolve(sourcePath);
            StorageLocation destinationLocation = _storage.Resolve(destinationPath);
            if(_storage.AreEquivalent(sourceLocation, destinationLocation) ||
               _storage.IsDescendant(sourceLocation, destinationLocation))
            {
                throw new InvalidOperationException( "Нельзя копировать каталог в самого себя или во вложенный подкаталог.");
            }

            if(src.Scheme.Equals(StorageLocation.LocalProviderId, StringComparison.OrdinalIgnoreCase) &&
               IsCrossVolumeLocalMove(src.NativePath, dst.NativePath))
            {
                FileOperationResult copyResult = await CopyAsync(
                    sourcePath,
                    destinationPath,
                    progress,
                    cancellationToken);
                if(!copyResult.Success)
                    return copyResult;
                long sourceSize = await _storage.GetTotalSizeAsync(
                    sourceLocation,
                    cancellationToken);
                long destinationSize = await _storage.GetTotalSizeAsync(
                    destinationLocation,
                    cancellationToken);
                if(await _storage.GetMetadataAsync(destinationLocation, cancellationToken) is null ||
                   sourceSize != destinationSize)
                {
                    return FileOperationResult.Fail(
                        "The copied item could not be verified; the source was not deleted.");
                }
                return await DeleteAsync(sourcePath, cancellationToken);
            }

            var provider = ResolveProvider(src.Scheme);
            try
            {
                return await provider.MoveAsync(src.NativePath, dst.NativePath, progress, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return FileOperationResult.Fail($"Move failed: {ex.Message}");
            }
        }

        public async Task<FileOperationResult> DeleteAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return await provider.DeleteAsync(desc.NativePath, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return FileOperationResult.Fail($"Delete failed: {ex.Message}");
            }
        }

        public async Task<FileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);
            try
            {
                return await provider.RenameAsync(desc.NativePath, newName, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return FileOperationResult.Fail($"Rename failed: {ex.Message}");
            }
        }

        public async Task<FileOperationResult> CreateDirectoryAsync(string parentDirectory, string newDirectoryName, CancellationToken cancellationToken)
        {
            // parentDirectory может быть "local://C:/Temp"
            // Нам надо сделать "C:/Temp/NewName"

            var parentDesc = ParsePath(parentDirectory);
            var provider = ResolveProvider(parentDesc.Scheme);

            StorageLocation combinedLocation = _storage.GetChild(
                _storage.Resolve(parentDirectory),
                newDirectoryName);
            string combinedNativePath = combinedLocation.OpaqueId;

            try
            {
                return await provider.CreateDirectoryAsync(combinedNativePath, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return FileOperationResult.Fail($"Create directory failed: {ex.Message}");
            }
        }

        public async Task<bool> CanReadAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return await provider.CanReadAsync(desc.NativePath, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception)
            {
                return false;
            }
        }

        public async Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return await provider.CanWriteAsync(desc.NativePath, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception)
            {
                return false;
            }
        }

        public async Task<Stream> OpenReadAsync(string path, IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return await provider.OpenReadAsync(desc.NativePath, progress, cancellationToken);
            }
            catch(FileNotFoundException)
            {
                throw; // пробрасываем дальше, чтобы можно было различать "файл не найден" и "другая ошибка"
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                throw new IOException($"OpenRead failed for path '{path}': {ex.Message}", ex);
            }
        }

        public async Task<Stream> OpenWriteAsync(string path, bool overwrite, IProgressReporter? progress = null, 
        CancellationToken cancellationToken = default)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return await provider.OpenWriteAsync(desc.NativePath, overwrite, progress, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                throw new IOException($"OpenWrite failed for path '{path}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Нормализуем путь:
        /// - если нет схемы → добавляем "local://"
        /// - если есть схема → возвращаем как есть, но без лишних пробелов и с унификацией слэшей
        /// </summary>
        /// <param name="rawPath"></param>
        /// <returns></returns>
        public string NormalizePath(string rawPath)
        {
            var desc = ParsePath(rawPath);

            // На этом уровне мы не будем "исправлять" обратные/прямые слэши.
            // Это ответственность конкретного провайдера.
            // Мы только формируем каноническое представление `<scheme>://<native>`.
            return $"{desc.Scheme}://{desc.NativePath}";
        }

        public Task<bool> IsHiddenAsync(string path, CancellationToken ct)
        {
            var desc = ParsePath(path);
            try
            {
                return ResolveProvider(desc.Scheme).IsHiddenAsync(desc.NativePath, ct);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception)
            {
                return Task.FromResult(false);
            }
        }

        public Task<FileOperationResult> SetHiddenAsync(string path, bool hidden, CancellationToken ct)
        {
            var desc = ParsePath(path);
            try
            {
                return ResolveProvider(desc.Scheme).SetHiddenAsync(desc.NativePath, hidden, ct);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return Task.FromResult(FileOperationResult.Fail($"SetHidden failed: {ex.Message}"));
            }
        }

        public Task<bool> IsReadOnlyAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            try
            {
                return ResolveProvider(desc.Scheme).IsReadOnlyAsync(desc.NativePath, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception)
            {
                return Task.FromResult(false);
            }
        }

        public Task<FileOperationResult> SetReadOnlyAsync(string path, bool isReadOnly, CancellationToken ct)
        {
            var desc = ParsePath(path);
            try
            {
                return ResolveProvider(desc.Scheme).SetReadOnlyAsync(desc.NativePath, isReadOnly, ct);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return Task.FromResult(FileOperationResult.Fail($"SetReadOnly failed: {ex.Message}"));
            }
        }



        // ------------------------
        // Внутренняя утилита
        // ------------------------


        internal readonly struct PathDescriptor
        {
            public string Scheme { get; }
            public string NativePath { get; } // часть после схемы

            public PathDescriptor(string scheme, string nativePath)
            {
                Scheme = scheme;
                NativePath = nativePath;
            }
        }

        private PathDescriptor ParsePath(string rawPath)
        {
            if(string.IsNullOrWhiteSpace(rawPath))
                throw new ArgumentException("Path is empty.", nameof(rawPath));

            // Ищем "://"
            int schemeSep = rawPath.IndexOf("://", StringComparison.Ordinal);
            if(schemeSep > 0)
            {
                string scheme = rawPath.Substring(0, schemeSep);
                string native = rawPath.Substring(schemeSep + 3); // после "://"
                if(string.IsNullOrWhiteSpace(native))
                    throw new ArgumentException("Path missing native part after scheme://", nameof(rawPath));

                return new PathDescriptor(scheme, native);
            }

            // Нет схемы => считаем, что это локальный путь
            return new PathDescriptor("local", rawPath);
        }

        private IFileProviderService ResolveProvider(string scheme)
        {
            if(_providersByScheme.TryGetValue(scheme, out var provider))
                return provider;

            throw new NotSupportedException($"No provider registered for scheme '{scheme}'.");
        }

        private static bool IsCrossVolumeLocalMove(
            string sourcePath,
            string destinationPath)
        {
            string? sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
            string? destinationRoot = Path.GetPathRoot(Path.GetFullPath(destinationPath));
            return !string.Equals(
                sourceRoot,
                destinationRoot,
                StringComparison.OrdinalIgnoreCase);
        }

    }
}

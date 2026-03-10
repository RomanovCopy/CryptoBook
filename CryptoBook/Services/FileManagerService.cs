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
    public sealed class FileManagerService:IFileManagerService
    {
        private readonly IReadOnlyDictionary<string, IFileProviderService> _providersByScheme;


        /// <summary>
        /// В конструктор передаем все зарегистрированные IFileSystemProvider.
        /// Autofac сам вольет сюда все реализации (LocalFileSystemProvider и будущие Zip/Ssh/...).
        /// </summary>
        public FileManagerService(IEnumerable<IFileProviderService> providers)
        {
            // Если два провайдера объявят одинаковую схему — это повод упасть сразу, а не в рантайме.
            _providersByScheme = providers
                .GroupBy(p => p.Scheme, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Single(),
                    StringComparer.OrdinalIgnoreCase
                );
        }


        public Task<List<ISystemItem>> BrowseAsync(string path, CancellationToken ct, bool includeHidden = false)
        {
            var desc = ParsePath(path); // как раньше                                           
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return provider.GetContainerContentAsync(desc.NativePath, ct, includeHidden);
            }
            catch(Exception ex)
            {
                throw new IOException($"Failed to browse path '{path}': {ex.Message}", ex);
            }
            
        }

        public async Task<FileOperationResult> CopyAsync(string sourcePath, string destinationPath, IProgressReporter? progress, CancellationToken cancellationToken)
        {
            var src = ParsePath(sourcePath);
            var dst = ParsePath(destinationPath);

            if(!string.Equals(src.Scheme, dst.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                // cross-scheme copy (например local -> ssh) в будущем можно поддержать здесь (stream copy),
                // но сейчас честно скажем, что не поддерживается
                return FileOperationResult.Fail("Copy across different providers is not supported yet.");
            }

            if(IsSameOrSubdirectory(sourcePath, destinationPath))
            {
                throw new InvalidOperationException(
                    "Нельзя копировать каталог в самого себя " +
                    "или во вложенный подкаталог.");
            }


            var provider = ResolveProvider(src.Scheme);
            try
            {
                return await provider.CopyAsync(src.NativePath, dst.NativePath, progress, cancellationToken);
            }
            catch(Exception ex)
            {
                return FileOperationResult.Fail($"Copy failed: {ex.Message}");
            }
        }

        public async Task<FileOperationResult> MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            var src = ParsePath(sourcePath);
            var dst = ParsePath(destinationPath);

            if(!string.Equals(src.Scheme, dst.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                // Теоретически можно сделать Copy+Delete,
                // но это уже "интеллектуальное перемещение".
                // Пока не лезем.
                return FileOperationResult.Fail("Move across different providers is not supported yet.");
            }

            if(IsSameOrSubdirectory(sourcePath,destinationPath))
            {
                throw new InvalidOperationException(
                    "Нельзя копировать каталог в самого себя " +
                    "или во вложенный подкаталог.");
            }

            var provider = ResolveProvider(src.Scheme);
            try
            {
                return await provider.MoveAsync(src.NativePath, dst.NativePath, cancellationToken);
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

            string combinedNativePath = CombinePath(provider, parentDesc.NativePath, newDirectoryName);

            try
            {
                return await provider.CreateDirectoryAsync(combinedNativePath, cancellationToken);
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
            catch(Exception)
            {
                return false;
            }
        }

        public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return await provider.OpenReadAsync(desc.NativePath, cancellationToken);
            }
            catch(Exception ex)
            {
                throw new IOException($"OpenRead failed for path '{path}': {ex.Message}", ex);
            }
        }

        public async Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            try
            {
                return await provider.OpenWriteAsync(desc.NativePath, overwrite, cancellationToken);
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
        /// <exception cref="NotImplementedException"></exception>
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
            catch(Exception ex)
            {
                return Task.FromResult(FileOperationResult.Fail($"SetReadOnly failed: {ex.Message}"));
            }
        }



        // ------------------------
        // Внутренняя утилита
        // ------------------------


        private bool IsSameOrSubdirectory(
        string sourceFull,
        string destFull)
        {
            if(string.Equals(sourceFull, destFull))
                return true;

            return destFull.StartsWith(
                sourceFull + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

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

        /// <summary>
        /// Логика склейки для "создать папку".
        /// Для local это Path.Combine.
        /// Для zip/ssh в будущем может быть другое поведение.
        /// Здесь мы делаем хак: если это local-провайдер, то Combine через System.IO.Path,
        /// иначе — просто parent + "/" + child.
        /// </summary>
        private static string CombinePath(IFileProviderService provider, string parent, string child)
        {
            if(provider.Scheme.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                return System.IO.Path.Combine(parent, child);
            }

            // fallback generic
            if(parent.EndsWith("/", StringComparison.Ordinal))
                return parent + child;
            return parent + "/" + child;
        }

    }
}

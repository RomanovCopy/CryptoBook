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
            return provider.GetDirectoryContentAsync(desc.NativePath, ct, includeHidden );
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

            var provider = ResolveProvider(src.Scheme);
            return await provider.CopyAsync(src.NativePath, dst.NativePath, progress, cancellationToken);
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

            var provider = ResolveProvider(src.Scheme);
            return await provider.MoveAsync(src.NativePath, dst.NativePath, cancellationToken);
        }

        public async Task<FileOperationResult> DeleteAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            return await provider.DeleteAsync(desc.NativePath, cancellationToken);
        }

        public async Task<FileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            return await provider.RenameAsync(desc.NativePath, newName, cancellationToken);
        }

        public async Task<FileOperationResult> CreateDirectoryAsync(string parentDirectory, string newDirectoryName, CancellationToken cancellationToken)
        {
            // parentDirectory может быть "local://C:/Temp"
            // Нам надо сделать "C:/Temp/NewName"

            var parentDesc = ParsePath(parentDirectory);
            var provider = ResolveProvider(parentDesc.Scheme);

            string combinedNativePath = CombinePath(provider, parentDesc.NativePath, newDirectoryName);

            return await provider.CreateDirectoryAsync(combinedNativePath, cancellationToken);
        }

        public async Task<bool> CanReadAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            return await provider.CanReadAsync(desc.NativePath, cancellationToken);
        }

        public async Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            return await provider.CanWriteAsync(desc.NativePath, cancellationToken);
        }

        public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            return await provider.OpenReadAsync(desc.NativePath, cancellationToken);
        }

        public async Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            var provider = ResolveProvider(desc.Scheme);

            return await provider.OpenWriteAsync(desc.NativePath, overwrite, cancellationToken);
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
            return ResolveProvider(desc.Scheme).IsHiddenAsync(desc.NativePath, ct);
        }

        public Task<FileOperationResult> SetHiddenAsync(string path, bool hidden, CancellationToken ct)
        {
            var desc = ParsePath(path);
            return ResolveProvider(desc.Scheme).SetHiddenAsync(desc.NativePath, hidden, ct);
        }

        public Task<bool> IsReadOnlyAsync(string path, CancellationToken cancellationToken)
        {
            var desc = ParsePath(path);
            return ResolveProvider(desc.Scheme).IsReadOnlyAsync(desc.NativePath, cancellationToken);
        }

        public Task<FileOperationResult> SetReadOnlyAsync(string path, bool isReadOnly, CancellationToken ct)
        {
            var desc = ParsePath(path);
            return ResolveProvider(desc.Scheme).SetReadOnlyAsync(desc.NativePath, isReadOnly, ct);
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

using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class FavoriteDirectoryPathPolicy: IFavoriteDirectoryPathPolicy
    {
        private readonly IFileManagerService _fileManagerService;

        public FavoriteDirectoryPathPolicy(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService
                ?? throw new ArgumentNullException(nameof(fileManagerService));
        }

        public string Normalize(string path)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к директории не задан.", nameof(path));

            string normalized = _fileManagerService.NormalizePath(path.Trim());
            int separatorIndex = normalized.IndexOf("://", StringComparison.Ordinal);
            if(separatorIndex < 0)
                return Path.TrimEndingDirectorySeparator(normalized);

            string scheme = normalized[..separatorIndex];
            string providerPath = normalized[(separatorIndex + 3)..];
            string trimmedPath = scheme.Equals("local", StringComparison.OrdinalIgnoreCase)
                ? Path.TrimEndingDirectorySeparator(providerPath)
                : providerPath.TrimEnd('/', '\\');
            return $"{scheme}://{trimmedPath}";
        }

        public string GetDefaultDisplayName(string normalizedPath)
        {
            string displayPath = GetDisplayPath(normalizedPath);
            string trimmed = Path.TrimEndingDirectorySeparator(displayPath);
            string name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name)
                ? Path.GetPathRoot(displayPath) ?? displayPath
                : name;
        }

        public string GetDisplayPath(string normalizedPath)
        {
            const string localPrefix = "local://";
            return normalizedPath.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase)
                ? normalizedPath[localPrefix.Length..]
                : normalizedPath;
        }

        public Task<bool> IsAvailableAsync(
            string normalizedPath,
            CancellationToken cancellationToken = default)
        {
            return _fileManagerService.CanReadAsync(normalizedPath, cancellationToken);
        }
    }
}

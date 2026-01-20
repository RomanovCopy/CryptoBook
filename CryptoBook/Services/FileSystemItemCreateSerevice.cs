using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class FileSystemItemCreateService:IFileSystemItemCreateService
    {
        private readonly Func<string, IRootItem> _createRoot;
        private readonly Func<string, IContainerFileSystemItem, IDirectoryItem> _createDirectory;
        private readonly Func<string, IContainerFileSystemItem, IFileItem> _createFile;

        public FileSystemItemCreateService(
            Func<string, IRootItem> createRoot,
            Func<string, IContainerFileSystemItem, IDirectoryItem> createDirectory,
            Func<string, IContainerFileSystemItem, IFileItem> createFile)
        {
            _createRoot = createRoot ?? throw new ArgumentNullException(nameof(createRoot));
            _createDirectory = createDirectory ?? throw new ArgumentNullException(nameof(createDirectory));
            _createFile = createFile ?? throw new ArgumentNullException(nameof(createFile));
        }

        public IRootItem CreateRoot(string rootPath)
        {
            var normalized = NormalizeRootPath(rootPath);
            return _createRoot(normalized);
        }

        public IDirectoryItem CreateDirectory(string path, IContainerFileSystemItem parent)
        {
            if(parent is null)
                throw new ArgumentNullException(nameof(parent));
            var normalized = NormalizePath(path);
            return _createDirectory(normalized, parent);
        }

        public IFileItem CreateFile(string path, IContainerFileSystemItem parent)
        {
            if(parent is null)
                throw new ArgumentNullException(nameof(parent));
            var normalized = NormalizePath(path);
            return _createFile(normalized, parent);
        }

        public IContainerFileSystemItem CreateContainerDirectory(string path, IContainerFileSystemItem parent)
            => CreateDirectory(path, parent);

        private static string NormalizePath(string path)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is null or empty.", nameof(path));

            // если бывают “виртуальные пути” — убрать GetFullPath
            return Path.GetFullPath(path);
        }

        private static string NormalizeRootPath(string rootPath)
        {
            if(string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("RootPath is null or empty.", nameof(rootPath));

            // Для корня диска важно иметь завершающий separator: "C:\"
            var full = Path.GetFullPath(rootPath);
            if(!full.EndsWith(Path.DirectorySeparatorChar))
                full += Path.DirectorySeparatorChar;

            return full;
        }
    }
}

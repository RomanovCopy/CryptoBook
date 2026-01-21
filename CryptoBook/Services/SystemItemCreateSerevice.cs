using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class SystemItemCreateService:ISystemItemCreateService
    {
        private readonly ILifetimeScope _scope;
        public SystemItemCreateService(ILifetimeScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public IDriveItem CreateRoot(string rootPath)
        {
            var normalized = NormalizeRootPath(rootPath);
            return _scope.Resolve<IDriveItem>(new NamedParameter("rootPath", normalized));
        }

        public IDirectoryItem CreateDirectory(string path, IContainerSystemItem parent)
        {
            if(parent is null)
                throw new ArgumentNullException(nameof(parent));

            var normalized = NormalizePath(path);

            return _scope.Resolve<IDirectoryItem>(
                new NamedParameter("path", normalized),
                new NamedParameter("parent", parent));
        }

        public IFileItem CreateFile(string path, IContainerSystemItem parent)
        {
            if(parent is null)
                throw new ArgumentNullException(nameof(parent));

            var normalized = NormalizePath(path);

            return _scope.Resolve<IFileItem>(
                new NamedParameter("path", normalized),
                new NamedParameter("parent", parent));
        }


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

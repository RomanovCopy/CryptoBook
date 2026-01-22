using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Graphics.DirectX.Direct3D11;

namespace CryptoBook.Services
{
    public class SystemItemCreateService: ISystemItemCreateService
    {
        private readonly ILifetimeScope _scope;
        public SystemItemCreateService(ILifetimeScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public IDriveItem CreateRoot(string rootPath)
        {
            var normalized = NormalizeRootPath(rootPath);
            var sourceDrive = new DriveInfo(normalized);
            if(!sourceDrive.IsReady)
                throw new IOException($"Drive '{normalized}' is not ready.");
            else if(!string.Equals(sourceDrive.RootDirectory.FullName, normalized, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Root path '{normalized}' is invalid.", nameof(rootPath));
            var root = _scope.Resolve<IDriveItem>(new NamedParameter("rootPath", normalized));
            root.Name = sourceDrive.Name;
            root.VolumeLabel = sourceDrive.VolumeLabel;
            root.DriveFormat = sourceDrive.DriveFormat;
            root.DriveType = sourceDrive.DriveType;
            root.AvailableFreeSpace = sourceDrive.AvailableFreeSpace;
            root.RootDirectory = sourceDrive.RootDirectory.ToString();
            root.TotalSize = sourceDrive.TotalSize;
            root.FullPath = sourceDrive.RootDirectory.FullName;
            root.LastWriteTimeUtc = sourceDrive.RootDirectory.LastWriteTimeUtc;
            return root;
        }

        public IDirectoryItem CreateDirectory(string path, ISystemItem parent)
        {
            var normalized = NormalizePath(path);
            if(parent is null)
                throw new ArgumentNullException(nameof(parent));
            var dirInfo = new DirectoryInfo(normalized);
            if(!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Directory '{normalized}' not found.");
            var dir = _scope.Resolve<IDirectoryItem>(new NamedParameter("path", normalized), new NamedParameter("parent", parent));
            dir.Name = dirInfo.Name;
            dir.FullPath = dirInfo.FullName;
            dir.Parent = parent;
            dir.IsExpanded = false;
            dir.LastWriteTimeUtc = dirInfo.LastWriteTimeUtc;
            dir.RootDirectory = dirInfo.Root.FullName;
            return dir;
        }

        public IFileItem CreateFile(string path, ISystemItem parent)
        {
            var normalized = NormalizePath(path);
            if(parent is null)
                throw new ArgumentNullException(nameof(parent));
            var fileInfo = new FileInfo(normalized);
            if(!fileInfo.Exists)
                throw new FileNotFoundException($"File '{normalized}' not found.");
            var file = _scope.Resolve<IFileItem>(new NamedParameter("path", normalized), new NamedParameter("parent", parent));
            file.Name = fileInfo.Name;
            file.Size = fileInfo.Length;
            file.Extension = fileInfo.Extension;
            file.IsHidden = (fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
            file.IsReadOnly = fileInfo.IsReadOnly;
            file.Parent = parent;
            file.FullPath = fileInfo.FullName;
            file.RootDirectory = fileInfo.Directory?.Root.FullName ?? string.Empty;
            return file;
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

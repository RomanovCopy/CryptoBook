using Autofac;

using CryptoBook.DTO;
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
        private readonly IDirectoryMonitoringService _directoryMonitoringService;
        public SystemItemCreateService(ILifetimeScope scope, IDirectoryMonitoringService directoryMonitoringService)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _directoryMonitoringService = directoryMonitoringService;
        }

        public IDriveItem CreateRoot(string rootPath)
        {
            var normalized = NormalizeRootPath(rootPath);
            var sourceDrive = new DriveInfo(normalized);
            if(!sourceDrive.IsReady)
                throw new IOException($"Drive '{normalized}' is not ready.");
            else if(!string.Equals(sourceDrive.RootDirectory.FullName, normalized, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Root path '{normalized}' is invalid.", nameof(rootPath));
            var root = _scope.Resolve<IDriveItem>();
            root.Name = sourceDrive.Name;
            root.VolumeLabel = sourceDrive.VolumeLabel;
            root.DriveFormat = sourceDrive.DriveFormat;
            root.DriveType = sourceDrive.DriveType;
            root.AvailableFreeSpace = sourceDrive.AvailableFreeSpace;
            root.RootDirectory = sourceDrive.RootDirectory.ToString();
            root.TotalSize = sourceDrive.TotalSize;
            root.FullPath = sourceDrive.RootDirectory.FullName;
            root.LastWriteTimeUtc = sourceDrive.RootDirectory.LastWriteTimeUtc.ToLocalTime();
            return root;
        }

        public IDirectoryItem CreateDirectory(string path, ISystemItem? parent)
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
            dir.LastWriteTimeUtc = dirInfo.LastWriteTimeUtc.ToLocalTime();
            dir.RootDirectory = dirInfo.Root.FullName;
            return dir;
        }

        public IFileItem CreateFile(string path, ISystemItem? parent)
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
            file.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc.ToLocalTime();
            return file;
        }


        private List<ISystemItem> SystemItemCreate(FileSystemEventArgs e, IContainerSystemItem container)
        {
            var items = new List<ISystemItem>();
            var path = e.FullPath;
            if(string.IsNullOrWhiteSpace(path) || !Path.Exists(path))
                return items;
            path = Path.GetFullPath(path);

            switch(e.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                {
                    var existing = container.Children.FirstOrDefault(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase));
                    if(existing != null)
                        items.Add(existing);
                    break;
                }
                case WatcherChangeTypes.Created:
                {
                    if(Directory.Exists(path) && !container.Children.Any(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        var dirItem = CreateDirectory(path, container);
                        items.Add(dirItem);
                    } else if(File.Exists(path) && !container.Children.Any(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        var fileItem = CreateFile(path, container);
                        items.Add(fileItem);
                    }
                    break;
                }
            }

            return items;
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

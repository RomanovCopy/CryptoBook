using CryptoBook.Interfaces;

using System.Collections;
using System.Collections.ObjectModel;
using System.IO;

namespace CryptoBook.Services
{
    public static class FileExplorerSelectionPolicy
    {
        public static IReadOnlyList<ISystemItem> CreateSnapshot(object? selection)
        {
            IEnumerable<ISystemItem> items = selection switch
            {
                ISystemItem item => [item],
                IEnumerable enumerable => enumerable.OfType<ISystemItem>(),
                _ => []
            };

            return new ReadOnlyCollection<ISystemItem>(items.ToArray());
        }

        public static IReadOnlyList<ISystemItem> NormalizeForOperation(object? selection)
        {
            IReadOnlyList<ISystemItem> snapshot = CreateSnapshot(selection);
            var unique = new List<ISystemItem>(snapshot.Count);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach(ISystemItem item in snapshot)
            {
                if(string.IsNullOrWhiteSpace(item.FullPath))
                    continue;

                string path = NormalizePath(item.FullPath);
                if(paths.Add(path))
                    unique.Add(item);
            }

            var selectedDirectories = unique
                .OfType<IContainerSystemItem>()
                .Select(item => (Item: item, Path: NormalizePath(item.FullPath)))
                .ToArray();

            ISystemItem[] normalized = unique
                .Where(item => !selectedDirectories.Any(directory =>
                    !ReferenceEquals(directory.Item, item) &&
                    IsDescendantPath(NormalizePath(item.FullPath), directory.Path)))
                .ToArray();

            return new ReadOnlyCollection<ISystemItem>(normalized);
        }

        public static bool IsSingle(object? selection) =>
            CreateSnapshot(selection).Count == 1;

        public static bool ContainsDrive(object? selection) =>
            CreateSnapshot(selection).Any(item => item is IDriveItem);

        private static bool IsDescendantPath(string candidatePath, string directoryPath)
        {
            if(string.Equals(candidatePath, directoryPath, StringComparison.OrdinalIgnoreCase))
                return false;

            string prefix = directoryPath.EndsWith(Path.DirectorySeparatorChar) ||
                            directoryPath.EndsWith(Path.AltDirectorySeparatorChar)
                ? directoryPath
                : directoryPath + Path.DirectorySeparatorChar;

            return candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            int schemeSeparator = path.IndexOf("://", StringComparison.Ordinal);
            string nativePath = schemeSeparator > 0
                ? path[(schemeSeparator + 3)..]
                : path;

            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(nativePath));
            }
            catch(Exception exception) when(
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return nativePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
    }
}

using CryptoBook.Interfaces;

using System;
using System.IO;

namespace CryptoBook.Services
{
    public static class FileExplorerItemFilter
    {
        public static bool Matches(
            ISystemItem item,
            string? filterText,
            string? rootPath = null)
        {
            ArgumentNullException.ThrowIfNull(item);

            string query = filterText?.Trim() ?? string.Empty;
            if(query.Length == 0)
                return true;

            string displayName = item is IFileItem file
                ? string.Concat(file.Name, file.Extension)
                : item.Name;
            if(displayName.Contains(
                query,
                StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if(string.IsNullOrWhiteSpace(rootPath))
                return false;

            try
            {
                string? directory = Path.GetDirectoryName(item.FullPath);
                if(string.IsNullOrWhiteSpace(directory))
                    return false;

                string relativeDirectory = Path.GetRelativePath(
                    rootPath,
                    directory);
                return relativeDirectory.Contains(
                    query,
                    StringComparison.CurrentCultureIgnoreCase);
            }
            catch(Exception exception) when(
                exception is ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }
}

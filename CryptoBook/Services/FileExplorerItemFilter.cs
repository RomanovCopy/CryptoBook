using CryptoBook.Interfaces;

using System;

namespace CryptoBook.Services
{
    public static class FileExplorerItemFilter
    {
        public static bool Matches(ISystemItem item, string? filterText)
        {
            ArgumentNullException.ThrowIfNull(item);

            string query = filterText?.Trim() ?? string.Empty;
            if(query.Length == 0)
                return true;

            string displayName = item is IFileItem file
                ? string.Concat(file.Name, file.Extension)
                : item.Name;
            return displayName.Contains(
                query,
                StringComparison.CurrentCultureIgnoreCase);
        }
    }
}

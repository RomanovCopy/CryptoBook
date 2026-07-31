using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class FileDisplayNameService: IFileDisplayNameService
    {
        public string GetDisplayName(
            string? pathOrName,
            string? defaultExtension = null)
        {
            if(string.IsNullOrWhiteSpace(pathOrName))
                return string.Empty;

            string displayName = Path.GetFileName(pathOrName.Trim());
            if(string.IsNullOrWhiteSpace(displayName))
                return string.Empty;

            if(!Path.HasExtension(displayName) &&
               !string.IsNullOrWhiteSpace(defaultExtension))
            {
                string extension = defaultExtension.StartsWith('.')
                    ? defaultExtension
                    : "." + defaultExtension;
                displayName += extension;
            }

            return displayName;
        }
    }
}

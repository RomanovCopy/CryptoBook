using CryptoBook.Infrastructure;

using System.IO;

namespace CryptoBook.Services
{
    internal static class ProtectedCopyPathResolver
    {
        public static string GetAvailablePath(
            string sourcePath,
            string destinationDirectory,
            ISet<string>? reservedPaths = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            if(string.IsNullOrWhiteSpace(baseName))
                throw new IOException(
                    LocalizationManager.GetString(
                        "Security.ProtectedCopyNameUnknown"));

            string desiredPath = Path.Combine(
                Path.GetFullPath(destinationDirectory),
                baseName + ".cbook");
            if(IsAvailable(desiredPath, reservedPaths))
                return desiredPath;

            for(int index = 2; ; index++)
            {
                string candidate = Path.Combine(
                    Path.GetDirectoryName(desiredPath)!,
                    $"{baseName} ({index}).cbook");
                if(IsAvailable(candidate, reservedPaths))
                    return candidate;
            }
        }

        private static bool IsAvailable(
            string path,
            ISet<string>? reservedPaths)
        {
            if(File.Exists(path) || Directory.Exists(path))
                return false;

            return reservedPaths is null || reservedPaths.Add(path);
        }
    }
}

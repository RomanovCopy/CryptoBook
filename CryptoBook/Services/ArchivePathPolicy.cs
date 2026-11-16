using System.IO;

namespace CryptoBook.Services;

internal static class ArchivePathPolicy
{
    public static bool IsSupportedArchive(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".7z" or ".rar";

    public static string GetArchiveType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".zip" => "zip", ".7z" => "7z", ".rar" => GetRarType(path),
        _ => throw new NotSupportedException(Text("Unsupported"))
    };

    private static string GetRarType(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[8];
        int count = stream.Read(signature);
        return count == 8 && signature.SequenceEqual(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 })
            ? "rar5" : "rar";
    }

    // Apply Windows rules even if the archive originated on another platform.
    public static string ValidateEntry(string value)
    {
        if(string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) ||
           value.StartsWith('/') || value.StartsWith('\\'))
            throw new InvalidDataException(Text("UnsafePath"));
        string[] parts = value.Replace('/', '\\').Split('\\');
        foreach(string part in parts)
        {
            string stem = part.Split('.')[0];
            if(part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
               part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
               stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                 (stem[3] is >= '0' and <= '9' or '¹' or '²' or '³')))
                throw new InvalidDataException(Text("UnsafePath"));
        }
        return string.Join(Path.DirectorySeparatorChar, parts);
    }

    public static string LocalPath(string value)
    {
        if(string.IsNullOrWhiteSpace(value) || value.Contains("://", StringComparison.Ordinal) ||
           !Path.IsPathFullyQualified(value))
            throw new ArgumentException(Text("LocalOnly"));
        string path = Path.GetFullPath(value);
        if(path.StartsWith("\\\\", StringComparison.Ordinal) || path.Any(char.IsControl))
            throw new ArgumentException(Text("LocalOnly"));
        if(new DriveInfo(Path.GetPathRoot(path)!).DriveType == DriveType.Network)
            throw new ArgumentException(Text("LocalOnly"));
        EnsureNoLinks(path);
        return Path.TrimEndingDirectorySeparator(path);
    }

    public static void EnsureNoLinks(string path)
    {
        for(string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(Text("LinksUnsupported"));
            }
            catch(FileNotFoundException) { }
            catch(DirectoryNotFoundException) { }
        }
    }

    public static bool IsWithin(string parent, string candidate) =>
        candidate.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    public static string Text(string name) => Infrastructure.LocalizationManager.GetString("Archive." + name);
}

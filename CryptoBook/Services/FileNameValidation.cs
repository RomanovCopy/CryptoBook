using CryptoBook.Infrastructure;

using System.IO;
using System.Text.RegularExpressions;

namespace CryptoBook.Services;

public static class FileNameValidation
{
    public static string? Validate(string? name)
    {
        if(string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." ||
           name.EndsWith('.') || name.EndsWith(' ') ||
           name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
           Regex.IsMatch(name.Split('.')[0].TrimEnd(' '),
               @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$",
               RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return LocalizationManager.GetString("File.InvalidName");
        return null;
    }

    public static string RenameFile(string name, string originalExtension) =>
        Path.HasExtension(name) ? name : name + originalExtension;
}

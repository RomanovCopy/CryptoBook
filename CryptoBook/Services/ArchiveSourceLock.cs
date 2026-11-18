using System.IO;
using System.Text.RegularExpressions;

namespace CryptoBook.Services;

/// <summary>Keep every local volume stable, not just the first file opened by 7-Zip.</summary>
internal sealed class ArchiveSourceLock: IDisposable
{
    private readonly List<FileStream> files = [];

    internal ArchiveSourceLock(string path)
    {
        try
        {
            string name = Path.GetFileName(path);
            string folder = Path.GetDirectoryName(path)!;
            string? pattern = ArchivePathPolicy.IsVolume(path) ? "^" + Regex.Escape(name[..^3]) + @"\d{3,}$" :
                Regex.IsMatch(name, @"\.part\d+\.rar$", RegexOptions.IgnoreCase) ?
                    "^" + Regex.Escape(Regex.Replace(name, @"\.part\d+\.rar$", "", RegexOptions.IgnoreCase)) + @"\.part\d+\.rar$" :
                Path.GetExtension(path).Equals(".rar", StringComparison.OrdinalIgnoreCase) ?
                    "^" + Regex.Escape(Path.GetFileNameWithoutExtension(name)) + @"\.(rar|r\d{2,})$" :
                Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase) ?
                    "^" + Regex.Escape(Path.GetFileNameWithoutExtension(name)) + @"\.(zip|z\d{2,})$" : null;
            string[] paths = pattern is null ? [path] : Directory.EnumerateFiles(folder)
                .Where(file => Regex.IsMatch(Path.GetFileName(file), pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToArray();
            if(paths.Length > 10_000) throw new IOException(ArchivePathPolicy.Text("TooManyEntries"));
            foreach(string file in paths)
            {
                ArchivePathPolicy.EnsureNoLinks(file);
                files.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            if(!paths.Contains(path, StringComparer.OrdinalIgnoreCase)) throw new FileNotFoundException(path);
        }
        catch { Dispose(); throw; }
    }

    public void Dispose() { foreach(var file in files) file.Dispose(); files.Clear(); }
}

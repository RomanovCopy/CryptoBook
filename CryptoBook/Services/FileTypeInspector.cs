using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace CryptoBook.Services;

/// <summary>Bounded signature inspection. Never loads, launches or decodes the inspected file.</summary>
public static class FileTypeInspector
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".heic", ".heif", ".avif", ".svg" };
    private static readonly HashSet<string> ActiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".exe", ".com", ".scr", ".dll", ".cpl", ".sys", ".msi", ".msp", ".msix", ".appx", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".lnk", ".url", ".reg", ".pif", ".jar" };

    public static async Task<FileTypeInspection> InspectAsync(
        string path, CancellationToken token = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess);
        byte[] header = new byte[4096];
        int length = await stream.ReadAtLeastAsync(header, header.Length, false, token);
        return await InspectHeaderAsync(stream, header.AsMemory(0, length),
            System.IO.Path.GetFileName(path), token);
    }

    private static async Task<FileTypeInspection> InspectHeaderAsync(
        Stream stream, ReadOnlyMemory<byte> data, string name, CancellationToken token)
    {
        string extension = System.IO.Path.GetExtension(name).ToLowerInvariant();
        var messages = new List<string>();
        bool warning = false;
        bool active = false;
        string type = LocalizationManager.GetString("FileDetails.UnknownType");
        string[] expected = [];
        if(data.Span.StartsWith("MZ"u8))
        {
            active = true;
            type = LocalizationManager.GetString("FileDetails.MzType");
            if(data.Length >= 64)
            {
                int offset = BinaryPrimitives.ReadInt32LittleEndian(data.Span[60..64]);
                if(offset >= 64 && offset <= stream.Length - 24)
                {
                    stream.Position = offset;
                    byte[] pe = new byte[24];
                    int read = await stream.ReadAtLeastAsync(pe, pe.Length, false, token);
                    if(read == pe.Length && pe.AsSpan().StartsWith("PE\0\0"u8))
                    {
                        bool dll = (BinaryPrimitives.ReadUInt16LittleEndian(pe.AsSpan(22, 2)) & 0x2000) != 0;
                        type = dll ? "Windows PE / DLL" : "Windows PE / EXE";
                    }
                }
            }
        }
        else if(data.Span.StartsWith(new byte[] { 0x4c, 0, 0, 0, 1, 0x14, 2, 0, 0, 0, 0, 0, 0xc0, 0, 0, 0, 0, 0, 0, 0x46 }))
        { type = "Windows Shell Link (.lnk)"; active = true; }
        else if(data.Span.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 13, 10, 26, 10 }))
        { type = "PNG"; expected = [".png"]; }
        else if(data.Span.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
        { type = "JPEG"; expected = [".jpg", ".jpeg", ".jfif"]; }
        else if(data.Span.StartsWith("GIF87a"u8) || data.Span.StartsWith("GIF89a"u8))
        { type = "GIF"; expected = [".gif"]; }
        else if(data.Span.StartsWith("BM"u8))
        { type = "BMP"; expected = [".bmp"]; }
        else if(data.Length >= 12 && data.Span[..4].SequenceEqual("RIFF"u8) && data.Span[8..12].SequenceEqual("WEBP"u8))
        { type = "WebP"; expected = [".webp"]; }
        else if(data.Span.StartsWith(new byte[] { 0x49, 0x49, 0x2a, 0 }) || data.Span.StartsWith(new byte[] { 0x4d, 0x4d, 0, 0x2a }))
        { type = "TIFF"; expected = [".tif", ".tiff"]; }
        else if(data.Span.StartsWith(new byte[] { 0, 0, 1, 0 }))
        { type = "ICO"; expected = [".ico"]; }
        else if(data.Span.StartsWith("%PDF-"u8))
        { type = "PDF"; expected = [".pdf"]; }
        else if(data.Span.StartsWith(new byte[] { 0x50, 0x4b, 3, 4 }) || data.Span.StartsWith(new byte[] { 0x50, 0x4b, 5, 6 }))
            type = "ZIP / Office / JAR / application package";
        else if(data.Span.StartsWith(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }))
            type = "OLE Compound File (Office / MSI / ...)";
        else if(data.Span.StartsWith(new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c }))
            type = "7-Zip";
        else if(data.Span.StartsWith(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 7 }))
            type = "RAR";
        else if(data.Span.StartsWith("#!"u8))
        { type = LocalizationManager.GetString("FileDetails.ScriptType"); active = true; }

        if(active)
        {
            warning = true;
            messages.Add(LocalizationManager.GetString(ImageExtensions.Contains(extension)
                ? "FileDetails.ExecutableAsImage" : "FileDetails.ActiveContent"));
        }
        if(ActiveExtensions.Contains(extension))
        {
            warning = true;
            messages.Add(LocalizationManager.Format("FileDetails.ActiveExtension", extension));
        }
        string innerExtension = System.IO.Path.GetExtension(System.IO.Path.GetFileNameWithoutExtension(name));
        if(ImageExtensions.Contains(innerExtension) && ActiveExtensions.Contains(extension))
        {
            warning = true;
            messages.Add(LocalizationManager.GetString("FileDetails.DoubleExtension"));
        }
        if(name.Any(c => c is '\u202a' or '\u202b' or '\u202c' or '\u202d' or '\u202e' or '\u2066' or '\u2067' or '\u2068' or '\u2069' or '\u200e' or '\u200f' or '\u061c' or '\u200b' or '\u200c' or '\u200d' or '\ufeff'))
        {
            warning = true;
            messages.Add(LocalizationManager.GetString("FileDetails.HiddenNameCharacters"));
        }
        bool knownImage = expected.Any(ImageExtensions.Contains);
        if(expected.Length > 0 && !expected.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            warning = true;
            messages.Add(LocalizationManager.Format("FileDetails.TypeMismatch", extension, type));
        }
        else if(ImageExtensions.Contains(extension) && !knownImage && !active)
        {
            warning = true;
            messages.Add(LocalizationManager.GetString("FileDetails.ImageUnconfirmed"));
        }
        messages.Add(LocalizationManager.GetString("FileDetails.ContentLimit"));
        return new FileTypeInspection(type, Convert.ToHexString(data.Span[..Math.Min(32, data.Length)]),
            warning, messages);
    }

    public static string EscapeName(string name)
    {
        var result = new StringBuilder();
        foreach(char character in name)
        {
            if(char.GetUnicodeCategory(character) is System.Globalization.UnicodeCategory.Format or System.Globalization.UnicodeCategory.Control)
                result.Append($"\\u{(int)character:X4}");
            else
                result.Append(character);
        }
        return result.ToString();
    }
}

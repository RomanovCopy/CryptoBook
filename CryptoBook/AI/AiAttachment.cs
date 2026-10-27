using CryptoBook.Interfaces;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;

namespace CryptoBook.AI;

public sealed record AiAttachment(string Name, string Text, ReadOnlyMemory<byte> ImageData,
    BitmapSource? Preview, long Size)
{
    public bool IsImage => !ImageData.IsEmpty;
    public bool IsBinary { get; init; }
    public string Detail => $"{Path.GetExtension(Name).TrimStart('.').ToUpperInvariant()} · {Math.Max(1, (long)Math.Ceiling(Size / 1024d))} KB";
}

public sealed class AiAttachmentService(IEnumerable<IDocumentTextExtractor> extractors) : IAiAttachmentService
{
    public const int MaxFiles = 4, MaxFileBytes = 32 * 1024 * 1024, MaxTextCharacters = 500_000, MaxBinaryPreviewBytes = 256;
    private readonly IDocumentTextExtractor[] extractors = extractors.ToArray();
    private static readonly HashSet<string> ImageExtensions = new([".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"], StringComparer.OrdinalIgnoreCase);

    public async Task<AiAttachment> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(path);
        var extractor = extractors.FirstOrDefault(item => item.CanExtract(extension));
        await using var file = File.OpenRead(path);
        byte[] bytes = await ReadBoundedAsync(file, MaxFileBytes, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if(ImageExtensions.Contains(extension)) return ReadImage(Path.GetFileName(path), bytes);
        string text;
        using var input = new MemoryStream(bytes, writable: false);
        if(extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)) text = await ReadDocxAsync(input, cancellationToken);
        else if(extractor is not null) text = await extractor.ExtractAsync(input, extension, cancellationToken);
        else
        {
            try
            {
                using var reader = new StreamReader(input, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
                text = await reader.ReadToEndAsync(cancellationToken);
            }
            catch(DecoderFallbackException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Binary(Path.GetFileName(path), bytes);
            }
            if(text.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
                return Binary(Path.GetFileName(path), bytes);
        }
        return Document(Path.GetFileName(path), text, bytes.Length);
    }

    private static AiAttachment Binary(string name, byte[] bytes)
    {
        int previewLength = Math.Min(bytes.Length, MaxBinaryPreviewBytes);
        var text = new StringBuilder();
        text.Append("Binary file: ").AppendLine(name)
            .Append("Size: ").Append(bytes.Length).AppendLine(" bytes.")
            .Append("Only the first ").Append(previewLength).AppendLine(" bytes are provided as a HEX preview.")
            .AppendLine("The full file content has not been extracted. Do not infer the unseen content.")
            .AppendLine("HEX dump (offset: bytes):");
        for(int offset = 0; offset < previewLength; offset += 16)
        {
            text.Append(offset.ToString("X8")).Append(':');
            for(int index = offset; index < Math.Min(offset + 16, previewLength); index++)
                text.Append(' ').Append(bytes[index].ToString("X2"));
            text.AppendLine();
        }
        return new(name, text.ToString(), ReadOnlyMemory<byte>.Empty, null, bytes.Length) { IsBinary = true };
    }

    public static AiAttachment Document(string name, string text, long? originalSize = null)
    {
        if(text.Length > MaxTextCharacters) throw new AiException("AI.AttachmentTooLarge");
        if(string.IsNullOrWhiteSpace(text)) throw new AiException("AI.AttachmentEmpty");
        return new(Path.GetFileName(name), text, ReadOnlyMemory<byte>.Empty, null, originalSize ?? Encoding.UTF8.GetByteCount(text));
    }

    private static AiAttachment ReadImage(string name, byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        int width = decoder.Frames[0].PixelWidth, height = decoder.Frames[0].PixelHeight;
        if(width <= 0 || height <= 0 || (long)width * height > 100_000_000) throw new AiException("AI.AttachmentTooLarge");
        input.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        if(width >= height) image.DecodePixelWidth = Math.Min(width, 1280);
        else image.DecodePixelHeight = Math.Min(height, 1280);
        image.StreamSource = input;
        image.EndInit();
        if(image.PixelWidth <= 0 || image.PixelHeight <= 0 || image.PixelHeight > 8192)
            throw new AiException("AI.UnsupportedAttachment");
        image.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var normalized = new MemoryStream();
        encoder.Save(normalized);
        return new(name, string.Empty, normalized.ToArray(), image, bytes.Length);
    }

    private static async Task<string> ReadDocxAsync(Stream input, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("word/document.xml") ?? throw new AiException("AI.UnsupportedAttachment");
        using var content = entry.Open();
        byte[] xml = await ReadBoundedAsync(content, 8 * 1024 * 1024, cancellationToken);
        using var stream = new MemoryStream(xml, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 });
        var document = XDocument.Load(reader);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join("\n", document.Descendants(word + "p").Select(paragraph => string.Concat(
            paragraph.Descendants().Select(element => element.Name == word + "t" ? element.Value :
                element.Name == word + "tab" ? "\t" : element.Name == word + "br" ? "\n" : ""))));
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, int limit, CancellationToken cancellationToken)
    {
        if(input.CanSeek && input.Length > limit) throw new AiException("AI.AttachmentTooLarge");
        using var output = new MemoryStream();
        byte[] buffer = new byte[65536];
        int count;
        while((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if(output.Length + count > limit) throw new AiException("AI.AttachmentTooLarge");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}

public static class AiVisionProjector
{
    public static string Find(string modelPath)
    {
        string? directory = Path.GetDirectoryName(modelPath);
        if(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            var files = Directory.EnumerateFiles(directory, "*.gguf", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).Contains("mmproj", StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if(files.Length == 1) return files[0];
        }
        throw new AiException("AI.ImageNeedsVisionModel");
    }
}

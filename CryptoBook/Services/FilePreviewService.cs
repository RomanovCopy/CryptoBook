using CryptoBook.DTO;
using CryptoBook.Interfaces;

using CryptoBook.Infrastructure;

using System.IO;
using System.Text;

namespace CryptoBook.Services
{
    public sealed class FilePreviewService: IFilePreviewService
    {
        internal const int MaxTextBytes = 512 * 1024;
        internal const int MaxImageBytes = 32 * 1024 * 1024;

        private static readonly HashSet<string> TextExtensions = new(
        [
            ".txt", ".log", ".md", ".cs", ".xaml", ".json", ".xml",
            ".yaml", ".yml", ".ini", ".config", ".csv", ".tsv"
        ], StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ImageExtensions = new(
        [
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"
        ], StringComparer.OrdinalIgnoreCase);

        private readonly IFilePreviewContentSource _contentSource;

        public FilePreviewService(IFilePreviewContentSource contentSource)
        {
            _contentSource = contentSource
                ?? throw new ArgumentNullException(nameof(contentSource));
        }

        public async Task<FilePreviewContent> LoadAsync(
            IFileItem file,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                bool encrypted = await _contentSource.IsEncryptedAsync(
                    file,
                    cancellationToken);
                if(encrypted)
                {
                    if(!_contentSource.HasDecryptionKey)
                    {
                        return new FilePreviewContent(
                            FilePreviewKind.Protected,
                            Message: LocalizationManager.GetString(
                                "Preview.EncryptedNeedsKey"));
                    }

                    return await LoadDecryptedAsync(file, cancellationToken);
                }

                string extension = NormalizeExtension(file.Extension, file.FullPath);
                if(TextExtensions.Contains(extension))
                    return await LoadTextAsync(file, cancellationToken);
                if(ImageExtensions.Contains(extension))
                    return await LoadImageAsync(file, cancellationToken);

                return new FilePreviewContent(
                    FilePreviewKind.Unsupported,
                    Message: extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                        ? LocalizationManager.GetString(
                            "Preview.PdfUnsupported")
                        : LocalizationManager.GetString(
                            "Preview.DetailsOnly"));
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return new FilePreviewContent(
                    FilePreviewKind.Error,
                    Message: LocalizationManager.Format(
                        file.Location.IsLocal
                            ? "Preview.DisplayFailed"
                            : "Preview.RemoteDisplayFailed",
                        Environment.NewLine,
                        ex.Message));
            }
        }

        private async Task<FilePreviewContent> LoadDecryptedAsync(
            IFileItem file,
            CancellationToken cancellationToken)
        {
            if(file.Size > MaxImageBytes)
            {
                return new FilePreviewContent(
                    FilePreviewKind.Unsupported,
                    Message: LocalizationManager.GetString(
                        "Preview.EncryptedTooLarge"));
            }

            try
            {
                // FileManager возвращает расшифрованный поток, если ключ уже установлен.
                await using Stream stream = await _contentSource.OpenReadAsync(
                    file,
                    cancellationToken);
                byte[] bytes = await ReadLimitedAsync(
                    stream,
                    MaxImageBytes,
                    cancellationToken);

                if(stream.CanSeek && stream.Position < stream.Length)
                {
                    return new FilePreviewContent(
                        FilePreviewKind.Unsupported,
                        Message: LocalizationManager.GetString(
                            "Preview.DecryptedTooLarge"));
                }

                if(IsSupportedImage(bytes))
                {
                    return new FilePreviewContent(
                        FilePreviewKind.Image,
                        ImageBytes: bytes);
                }

                if(IsPdf(bytes))
                {
                    return new FilePreviewContent(
                        FilePreviewKind.Unsupported,
                        Message: LocalizationManager.GetString(
                            "Preview.DecryptedPdfUnsupported"));
                }

                if(LooksLikeText(bytes))
                {
                    int textLength = Math.Min(bytes.Length, MaxTextBytes);
                    string suffix = bytes.Length > MaxTextBytes
                        ? Environment.NewLine + Environment.NewLine +
                            LocalizationManager.GetString(
                                "Preview.Truncated")
                        : string.Empty;
                    return new FilePreviewContent(
                        FilePreviewKind.Text,
                        Text: DecodeText(bytes.AsSpan(0, textLength).ToArray()) + suffix);
                }

                return new FilePreviewContent(
                    FilePreviewKind.Unsupported,
                    Message: LocalizationManager.GetString(
                        "Preview.DecryptedTypeUnsupported"));
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                return new FilePreviewContent(
                    FilePreviewKind.Error,
                    Message: LocalizationManager.Format(
                        "Preview.DecryptFailed",
                        Environment.NewLine,
                        ex.Message));
            }
        }

        private async Task<FilePreviewContent> LoadTextAsync(
            IFileItem file,
            CancellationToken cancellationToken)
        {
            await using Stream stream = await _contentSource.OpenReadAsync(
                file,
                cancellationToken);
            byte[] bytes = await ReadLimitedAsync(
                stream,
                MaxTextBytes,
                cancellationToken);
            string text = DecodeText(bytes);
            string? suffix = stream.CanSeek && stream.Position < stream.Length
                ? Environment.NewLine + Environment.NewLine +
                    LocalizationManager.GetString("Preview.Truncated")
                : null;
            return new FilePreviewContent(
                FilePreviewKind.Text,
                Text: text + suffix);
        }

        private async Task<FilePreviewContent> LoadImageAsync(
            IFileItem file,
            CancellationToken cancellationToken)
        {
            if(file.Size > MaxImageBytes)
            {
                return new FilePreviewContent(
                    FilePreviewKind.Unsupported,
                    Message: LocalizationManager.GetString(
                        "Preview.ImageTooLarge"));
            }

            await using Stream stream = await _contentSource.OpenReadAsync(
                file,
                cancellationToken);
            byte[] bytes = await ReadLimitedAsync(
                stream,
                MaxImageBytes,
                cancellationToken);
            if(stream.CanSeek && stream.Position < stream.Length)
            {
                return new FilePreviewContent(
                    FilePreviewKind.Unsupported,
                    Message: LocalizationManager.GetString(
                        "Preview.ImageTooLarge"));
            }

            return new FilePreviewContent(
                FilePreviewKind.Image,
                ImageBytes: bytes);
        }

        private static async Task<byte[]> ReadLimitedAsync(
            Stream stream,
            int limit,
            CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream(Math.Min(limit, 64 * 1024));
            byte[] chunk = new byte[16 * 1024];
            int remaining = limit;
            while(remaining > 0)
            {
                int read = await stream.ReadAsync(
                    chunk.AsMemory(0, Math.Min(chunk.Length, remaining)),
                    cancellationToken);
                if(read == 0)
                    break;

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
                remaining -= read;
            }

            return buffer.ToArray();
        }

        private static string DecodeText(byte[] bytes)
        {
            if(bytes.Length >= 3 &&
               bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if(bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if(bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            return Encoding.UTF8.GetString(bytes);
        }

        private static bool IsSupportedImage(ReadOnlySpan<byte> bytes)
        {
            return bytes.StartsWith(
                       new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ||
                   bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) ||
                   bytes.StartsWith("BM"u8) ||
                   bytes.StartsWith("GIF87a"u8) ||
                   bytes.StartsWith("GIF89a"u8) ||
                   (bytes.Length >= 12 &&
                    bytes[..4].SequenceEqual("RIFF"u8) &&
                    bytes.Slice(8, 4).SequenceEqual("WEBP"u8));
        }

        private static bool IsPdf(ReadOnlySpan<byte> bytes) =>
            bytes.StartsWith("%PDF-"u8);

        private static bool LooksLikeText(ReadOnlySpan<byte> bytes)
        {
            if(bytes.IsEmpty)
                return true;
            if(bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ||
               bytes.StartsWith(new byte[] { 0xFF, 0xFE }) ||
               bytes.StartsWith(new byte[] { 0xFE, 0xFF }))
                return true;

            ReadOnlySpan<byte> sample = bytes[..Math.Min(bytes.Length, MaxTextBytes)];
            int controlCharacters = 0;
            foreach(byte value in sample)
            {
                if(value == 0)
                    return false;
                if(value < 0x20 && value is not (0x09 or 0x0A or 0x0D))
                    controlCharacters++;
            }

            if(controlCharacters > Math.Max(2, sample.Length / 100))
                return false;

            try
            {
                _ = new UTF8Encoding(false, true).GetString(
                    sample);
                return true;
            }
            catch(DecoderFallbackException)
            {
                return false;
            }
        }

        private static string NormalizeExtension(string? extension, string path)
        {
            string value = string.IsNullOrWhiteSpace(extension)
                ? Path.GetExtension(path)
                : extension;
            return value.StartsWith('.') ? value : $".{value}";
        }
    }
}

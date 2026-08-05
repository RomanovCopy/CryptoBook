using CryptoBook.Interfaces;

using System.IO;
using System.Text;

namespace CryptoBook.Services
{
    public sealed class PlainTextDocumentTextExtractor:
        IDocumentTextExtractor
    {
        private static readonly HashSet<string> Extensions = new(
            [".txt", ".log", ".md", ".cs", ".xaml", ".json", ".xml"],
            StringComparer.OrdinalIgnoreCase);

        public bool CanExtract(string extension) =>
            Extensions.Contains(extension);

        public async Task<string> ExtractAsync(
            Stream content,
            string extension,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            if(!CanExtract(extension))
                throw new NotSupportedException(
                    $"The '{extension}' format is not supported by this extractor.");

            using var reader = new StreamReader(
                content,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 81920,
                leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}

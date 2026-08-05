using System.IO;

namespace CryptoBook.Security
{
    /// <summary>
    /// Расшифрованное содержимое вместе с исходным расширением.
    /// </summary>
    public sealed class DecryptedFileContent: IAsyncDisposable
    {
        public DecryptedFileContent(Stream content, string originalExtension)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            OriginalExtension = string.IsNullOrWhiteSpace(originalExtension)
                ? throw new ArgumentException(
                    "Original extension is required.",
                    nameof(originalExtension))
                : originalExtension;
        }

        public Stream Content { get; }
        public string OriginalExtension { get; }

        public ValueTask DisposeAsync() => Content.DisposeAsync();
    }
}

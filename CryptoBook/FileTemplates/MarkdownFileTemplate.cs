using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Text;

namespace CryptoBook.FileTemplates
{
    /// <summary>
    /// Markdown is a source-text format. Its formatted FlowDocument is always
    /// a disposable preview and is never used for persistence.
    /// </summary>
    public sealed class MarkdownFileTemplate: IFileTemplate
    {
        public string Id => "Markdown";
        public string DisplayName =>
            LocalizationManager.GetString("FileTemplate.Markdown");
        public string DefaultExtension => ".md";
        public IReadOnlyCollection<string> Extensions => [".md"];
        public string SuggestedBaseName =>
            LocalizationManager.GetString("FileTemplate.NewFile");
        public bool PreservesTextFormatting => false;
        public Encoding? DefaultEncoding => new UTF8Encoding(false);

        public Task<byte[]> GetInitialContentAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<byte>());
    }
}

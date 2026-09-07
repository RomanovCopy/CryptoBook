using System.ComponentModel;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Holds the authoritative source of the active Markdown document.
    /// </summary>
    public interface IMarkdownDocumentState:
        IService,
        INotifyPropertyChanged
    {
        bool IsActive { get; }
        string Text { get; set; }
        string? SourceDirectory { get; }
        long DocumentVersion { get; }

        byte[] GetBytes();
        void Activate(
            MarkdownTextDocument document,
            string? filePath);
        void Deactivate();
        void UpdateFilePath(string? filePath);
    }

    public sealed record MarkdownTextDocument(
        string Text,
        System.Text.Encoding Encoding,
        byte[] Preamble);
}

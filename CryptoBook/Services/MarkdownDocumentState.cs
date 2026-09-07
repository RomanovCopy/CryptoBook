using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Text;
using System.IO;

namespace CryptoBook.Services
{
    public sealed class MarkdownDocumentState:
        ViewModelBase,
        IMarkdownDocumentState
    {
        private string text = string.Empty;
        private string? sourceDirectory;
        private Encoding encoding = new UTF8Encoding(false);
        private byte[] preamble = [];
        private bool isActive;
        private long documentVersion;

        public bool IsActive
        {
            get => isActive;
            private set => SetProperty(ref isActive, value);
        }

        public string Text
        {
            get => text;
            set => SetProperty(ref text, value ?? string.Empty);
        }

        public string? SourceDirectory
        {
            get => sourceDirectory;
            private set => SetProperty(ref sourceDirectory, value);
        }

        public long DocumentVersion
        {
            get => documentVersion;
            private set => SetProperty(ref documentVersion, value);
        }

        public byte[] GetBytes()
        {
            if(!IsActive)
                throw new InvalidOperationException(
                    "Markdown document is not active.");

            return MarkdownTextCodec.Encode(new MarkdownTextDocument(
                Text,
                encoding,
                preamble));
        }

        public void Activate(
            MarkdownTextDocument document,
            string? filePath)
        {
            ArgumentNullException.ThrowIfNull(document);
            encoding = document.Encoding;
            preamble = document.Preamble.ToArray();
            Text = document.Text;
            UpdateFilePath(filePath);
            IsActive = true;
            DocumentVersion = checked(DocumentVersion + 1);
        }

        public void Deactivate()
        {
            if(!IsActive && Text.Length == 0 && SourceDirectory is null)
                return;

            IsActive = false;
            text = string.Empty;
            sourceDirectory = null;
            encoding = new UTF8Encoding(false);
            preamble = [];
            DocumentVersion = checked(DocumentVersion + 1);
            OnPropertyChanged(nameof(Text), nameof(SourceDirectory));
        }

        public void UpdateFilePath(string? filePath)
        {
            SourceDirectory = string.IsNullOrWhiteSpace(filePath)
                ? null
                : Path.GetDirectoryName(Path.GetFullPath(filePath));
        }
    }
}

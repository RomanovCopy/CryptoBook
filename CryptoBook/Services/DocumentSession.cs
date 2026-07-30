using CryptoBook.Interfaces;

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace CryptoBook.Services
{
    public sealed class DocumentSession: IDocumentSession
    {
        private string? filePath;
        private string displayName = string.Empty;
        private IFileTemplate? template;
        private bool isDirty;

        public DocumentSession(IRichTextBoxService richTextBox)
        {
            ArgumentNullException.ThrowIfNull(richTextBox);
            richTextBox.Service.TextChanged += OnDocumentChanged;
        }

        public string? FilePath
        {
            get => filePath;
            private set
            {
                if(string.Equals(
                    filePath,
                    value,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                filePath = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName
        {
            get => displayName;
            private set
            {
                if(string.Equals(displayName, value, StringComparison.Ordinal))
                    return;

                displayName = value;
                OnPropertyChanged();
            }
        }

        public IFileTemplate? Template
        {
            get => template;
            private set
            {
                if(ReferenceEquals(template, value))
                    return;

                template = value;
                OnPropertyChanged();
            }
        }

        public bool IsDirty
        {
            get => isDirty;
            private set
            {
                if(isDirty == value)
                    return;

                isDirty = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Open(string filePath, IFileTemplate template)
        {
            MarkSaved(filePath, template);
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void MarkSaved(
            string filePath,
            IFileTemplate template)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(template);

            FilePath = Path.GetFullPath(filePath);
            DisplayName = Path.GetFileName(FilePath);
            Template = template;
            IsDirty = false;
        }

        public void Rename(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            FilePath = Path.GetFullPath(filePath);
            DisplayName = Path.GetFileName(FilePath);
        }

        public void SetDisplayName(string displayName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            DisplayName = Path.GetFileName(displayName.Trim());
        }

        private void OnDocumentChanged(
            object sender,
            TextChangedEventArgs args) =>
            MarkDirty();

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
    }
}

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
            Template = template;
            IsDirty = false;
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

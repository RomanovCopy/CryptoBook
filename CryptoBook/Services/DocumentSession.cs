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
        private long revision;
        private long savedRevision;

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

        public bool IsDirty => Revision != SavedRevision;

        public long Revision
        {
            get => revision;
            private set
            {
                if(revision == value)
                    return;

                bool wasDirty = IsDirty;
                revision = value;
                OnPropertyChanged();
                if(wasDirty != IsDirty)
                    OnPropertyChanged(nameof(IsDirty));
            }
        }

        public long SavedRevision
        {
            get => savedRevision;
            private set
            {
                if(savedRevision == value)
                    return;

                bool wasDirty = IsDirty;
                savedRevision = value;
                OnPropertyChanged();
                if(wasDirty != IsDirty)
                    OnPropertyChanged(nameof(IsDirty));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Open(string filePath, IFileTemplate template)
        {
            MarkSaved(filePath, template);
        }

        public void MarkDirty()
        {
            Revision = checked(Revision + 1);
        }

        public void MarkSaved(
            string filePath,
            IFileTemplate template)
        {
            MarkSaved(filePath, template, Revision);
        }

        public void MarkSaved(
            string filePath,
            IFileTemplate template,
            long savedRevision)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(template);
            if(savedRevision < 0 || savedRevision > Revision)
                throw new ArgumentOutOfRangeException(nameof(savedRevision));

            FilePath = Path.GetFullPath(filePath);
            DisplayName = Path.GetFileName(FilePath);
            Template = template;
            SavedRevision = savedRevision;
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

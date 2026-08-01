using CryptoBook.Interfaces;

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace CryptoBook.Services
{
    /// <summary>
    /// Единый источник состояния текущего документа: пути, отображаемого имени,
    /// формата и ревизий, по которым определяется наличие несохранённых изменений.
    /// </summary>
    public sealed class DocumentSession: IDocumentSession
    {
        private readonly IRichTextBoxService richTextBox;
        private string? filePath;
        private string displayName = string.Empty;
        private IFileTemplate? template;
        private long revision;
        private long savedRevision;
        private bool suppressDocumentChanges;

        public DocumentSession(IRichTextBoxService richTextBox)
        {
            this.richTextBox = richTextBox
                ?? throw new ArgumentNullException(nameof(richTextBox));
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
                OnPropertyChanged(nameof(HasDocument));
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
                OnPropertyChanged(nameof(HasDocument));
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
                OnPropertyChanged(nameof(HasDocument));
            }
        }

        public bool IsDirty => Revision != SavedRevision;

        public bool HasDocument =>
            IsDirty ||
            !string.IsNullOrWhiteSpace(FilePath) ||
            !string.IsNullOrWhiteSpace(DisplayName) ||
            Template is not null;

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
                {
                    OnPropertyChanged(nameof(IsDirty));
                    OnPropertyChanged(nameof(HasDocument));
                }
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
                {
                    OnPropertyChanged(nameof(IsDirty));
                    OnPropertyChanged(nameof(HasDocument));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Open(string filePath, IFileTemplate template)
        {
            MarkSaved(filePath, template);
        }

        public void Close()
        {
            suppressDocumentChanges = true;
            try
            {
                richTextBox.ClearDocument();
            }
            finally
            {
                suppressDocumentChanges = false;
            }

            filePath = null;
            displayName = string.Empty;
            template = null;
            revision = 0;
            savedRevision = 0;
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Template));
            OnPropertyChanged(nameof(Revision));
            OnPropertyChanged(nameof(SavedRevision));
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(HasDocument));
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

            // Сохраняем именно переданную ревизию: пока шла запись на диск,
            // пользователь мог продолжить редактирование и увеличить Revision.
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
            TextChangedEventArgs args)
        {
            // Загрузка и программная очистка временно подавляют TextChanged,
            // иначе только что открытый документ сразу считался бы изменённым.
            if(!suppressDocumentChanges)
                MarkDirty();
        }

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
    }
}

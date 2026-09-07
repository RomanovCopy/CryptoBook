using CryptoBook.Interfaces;
using CryptoBook.FileTemplates;

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    /// <summary>
    /// Активный документ для файловых команд и независимое состояние второго
    /// редактора: содержимое, путь, формат и ревизии несохранённых изменений.
    /// </summary>
    public sealed class DocumentSession: IDocumentSession, IWorkspaceDocumentSession
    {
        private readonly IRichTextBoxService richTextBox;
        private readonly IMarkdownDocumentState? markdownDocument;
        private readonly IBookmarkService? bookmarks;
        private string? filePath;
        private string displayName = string.Empty;
        private IFileTemplate? template;
        private long revision;
        private long savedRevision;
        private bool suppressDocumentChanges;
        private bool suppressMarkdownChanges;
        private ParkedDocument? inactiveDocument;
        private int activeDocumentHolds;

        public IDisposable HoldActiveDocument()
        {
            activeDocumentHolds++;
            return new ActiveDocumentHold(this);
        }

        private sealed class ActiveDocumentHold(DocumentSession owner) : IDisposable
        {
            private DocumentSession? session = owner;
            public void Dispose()
            {
                if(session is null) return;
                session.activeDocumentHolds--;
                session = null;
            }
        }

        private sealed record ParkedDocument(
            FlowDocument Document, bool IsReadOnly, MarkdownTextDocument? Markdown,
            string? FilePath, string DisplayName, IFileTemplate? Template,
            long Revision, long SavedRevision);

        public string ActivePageKey => markdownDocument?.IsActive == true
            ? "MarkdownEditor" : "Home";
        public bool HasHomeDocument => ActivePageKey == "Home"
            ? HasDocument : inactiveDocument is { Markdown: null };
        public bool HasMarkdownDocument => ActivePageKey == "MarkdownEditor"
            ? HasDocument : inactiveDocument?.Markdown is not null;
        public bool HasInactiveChanges => inactiveDocument is { } parked &&
            parked.Revision != parked.SavedRevision;

        private ParkedDocument CaptureCurrent() => new(
            richTextBox.Document, richTextBox.IsReadOnly,
            markdownDocument?.IsActive == true
                ? MarkdownTextCodec.Decode(markdownDocument.GetBytes()) : null,
            FilePath, DisplayName, Template, Revision, SavedRevision);

        public bool SelectPage(string pageKey)
        {
            if(pageKey == ActivePageKey)
                return true;
            if(pageKey is not ("Home" or "MarkdownEditor") ||
               inactiveDocument is null || activeDocumentHolds > 0)
                return false;

            ParkedDocument target = inactiveDocument;
            inactiveDocument = HasDocument ? CaptureCurrent() : null;
            RestoreCurrent(target);
            return true;
        }

        private void RestoreCurrent(ParkedDocument target)
        {
            suppressDocumentChanges = suppressMarkdownChanges = true;
            try
            {
                richTextBox.ReplaceDocument(target.Document);
                richTextBox.IsReadOnly = target.IsReadOnly;
                bookmarks?.RebuildIndexFromDocument(richTextBox);
                if(target.Markdown is not null)
                    markdownDocument?.Activate(target.Markdown, target.FilePath);
                else
                    markdownDocument?.Deactivate();
                filePath = target.FilePath;
                displayName = target.DisplayName;
                template = target.Template;
                revision = target.Revision;
                savedRevision = target.SavedRevision;
            }
            finally
            {
                suppressDocumentChanges = suppressMarkdownChanges = false;
            }
            NotifyCurrentDocumentChanged();
        }

        private void NotifyCurrentDocumentChanged()
        {
            foreach(string property in new[] { nameof(FilePath), nameof(DisplayName),
                nameof(Template), nameof(Revision), nameof(SavedRevision),
                nameof(IsDirty), nameof(HasDocument), nameof(HasHomeDocument),
                nameof(HasMarkdownDocument), nameof(HasInactiveChanges), nameof(ActivePageKey) })
                OnPropertyChanged(property);
        }

        public void CloseCurrent()
        {
            if(activeDocumentHolds > 0)
                return;
            ParkedDocument? remaining = inactiveDocument;
            if(remaining is not null)
            {
                inactiveDocument = null;
                RestoreCurrent(remaining);
            }
            else
                Close();
        }

        public WorkspaceDocumentSnapshot? CaptureInactiveDocument()
        {
            if(inactiveDocument is not { } parked)
                return null;
            byte[] content;
            if(parked.Markdown is not null)
                content = MarkdownTextCodec.Encode(parked.Markdown);
            else
            {
                using var stream = new MemoryStream();
                new TextRange(parked.Document.ContentStart, parked.Document.ContentEnd)
                    .Save(stream, System.Windows.DataFormats.XamlPackage);
                content = stream.ToArray();
            }
            return new(parked.FilePath, parked.DisplayName, parked.Template?.Id,
                parked.Revision, parked.SavedRevision, parked.Markdown is not null, content,
                parked.IsReadOnly);
        }

        public void RestoreInactiveDocument(WorkspaceDocumentSnapshot? snapshot)
        {
            if(snapshot is null)
                return;
            var document = new FlowDocument();
            MarkdownTextDocument? markdown = null;
            if(snapshot.IsMarkdown)
            {
                markdown = MarkdownTextCodec.Decode(snapshot.Content);
                document.Blocks.Add(new Paragraph(new Run(markdown.Text)));
                MarkdownDocumentMetadata.SetSource(document, markdown);
            }
            else
            {
                using var stream = new MemoryStream(snapshot.Content, writable: false);
                new TextRange(document.ContentStart, document.ContentEnd)
                    .Load(stream, System.Windows.DataFormats.XamlPackage);
            }
            IFileTemplate? restoredTemplate = snapshot.TemplateId switch
            {
                "Markdown" => new MarkdownFileTemplate(),
                "Encrypted file" => new SecureFileTemplate(),
                "Text" => new PlainTextTemplate(),
                "rtf" => new RichTextFileTemplate(),
                "Xaml" => new XamlFileTemplate(),
                "XamlPackage" => new XamlPackageFileTemplate(),
                _ => null
            };
            // Preserve registered formats when restoring a parked document.
            restoredTemplate ??= templateRegistry?.GetById(snapshot.TemplateId ?? "");
            var restored = new ParkedDocument(document, snapshot.IsReadOnly, markdown,
                snapshot.FilePath, snapshot.DisplayName, restoredTemplate,
                snapshot.Revision, snapshot.SavedRevision);
            if(!HasDocument)
            {
                RestoreCurrent(restored);
                return;
            }
            if((markdown is not null) == (markdownDocument?.IsActive == true))
                throw new InvalidDataException("Workspace documents must belong to different editors.");
            inactiveDocument = restored;
            NotifyCurrentDocumentChanged();
        }

        private readonly IFileTemplateRegistry? templateRegistry;

        public DocumentSession(
            IRichTextBoxService richTextBox,
            IMarkdownDocumentState? markdownDocument = null,
            IFileTemplateRegistry? templateRegistry = null,
            IBookmarkService? bookmarks = null)
        {
            this.richTextBox = richTextBox
                ?? throw new ArgumentNullException(nameof(richTextBox));
            this.markdownDocument = markdownDocument;
            this.templateRegistry = templateRegistry;
            this.bookmarks = bookmarks;
            richTextBox.Service.TextChanged += OnDocumentChanged;
            if(markdownDocument is not null)
                markdownDocument.PropertyChanged += OnMarkdownDocumentChanged;
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
            if(activeDocumentHolds > 0)
                throw new InvalidOperationException("A document operation is still in progress.");
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(template);
            if(markdownDocument is not null)
            {
                suppressMarkdownChanges = true;
                try
                {
                    if(markdownDocument.IsActive &&
                       (template is MarkdownFileTemplate or
                        SecureFileTemplate))
                    {
                        markdownDocument.UpdateFilePath(filePath);
                        richTextBox.IsReadOnly = true;
                    }
                    else if(template is not SecureFileTemplate)
                    {
                        markdownDocument.Deactivate();
                        richTextBox.IsReadOnly = false;
                    }
                }
                finally
                {
                    suppressMarkdownChanges = false;
                }
            }
            MarkSaved(filePath, template);
        }

        public void Open(
            string filePath,
            IFileTemplate template,
            FlowDocument document)
        {
            if(activeDocumentHolds > 0)
                throw new InvalidOperationException("A document operation is still in progress.");
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(document);

            FlowDocument previousDocument = richTextBox.Document;
            bool previousIsReadOnly = richTextBox.IsReadOnly;
            MarkdownTextDocument? previousMarkdown =
                markdownDocument?.IsActive == true
                    ? MarkdownTextCodec.Decode(markdownDocument.GetBytes())
                    : null;
            string? previousMarkdownPath = FilePath;
            MarkdownTextDocument? markdownSource =
                MarkdownDocumentMetadata.GetSource(document);
            ParkedDocument? previousInactive = inactiveDocument;
            if((markdownSource is not null) != (markdownDocument?.IsActive == true))
                inactiveDocument = HasDocument ? CaptureCurrent() : null;
            suppressDocumentChanges = true;
            suppressMarkdownChanges = true;
            try
            {
                richTextBox.ReplaceDocument(document);
                if(markdownDocument is not null)
                {
                    if(markdownSource is not null)
                    {
                        markdownDocument.Activate(markdownSource, filePath);
                        richTextBox.IsReadOnly = true;
                    }
                    else
                    {
                        markdownDocument.Deactivate();
                        richTextBox.IsReadOnly = false;
                    }
                }
                MarkSaved(filePath, template);
            }
            catch
            {
                inactiveDocument = previousInactive;
                if(!ReferenceEquals(richTextBox.Document, previousDocument))
                    richTextBox.ReplaceDocument(previousDocument);
                richTextBox.IsReadOnly = previousIsReadOnly;
                if(markdownDocument is not null)
                {
                    if(previousMarkdown is not null)
                    {
                        markdownDocument.Activate(
                            previousMarkdown,
                            previousMarkdownPath);
                    }
                    else
                    {
                        markdownDocument.Deactivate();
                    }
                }
                throw;
            }
            finally
            {
                suppressDocumentChanges = false;
                suppressMarkdownChanges = false;
            }
            NotifyCurrentDocumentChanged();
        }

        public void Close()
        {
            inactiveDocument = null;
            suppressDocumentChanges = true;
            try
            {
                richTextBox.ClearDocument();
                markdownDocument?.Deactivate();
                richTextBox.IsReadOnly = false;
            }
            finally
            {
                suppressDocumentChanges = false;
                suppressMarkdownChanges = false;
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
            NotifyCurrentDocumentChanged();
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
            if(markdownDocument?.IsActive == true)
            {
                suppressMarkdownChanges = true;
                try
                {
                    markdownDocument.UpdateFilePath(FilePath);
                }
                finally
                {
                    suppressMarkdownChanges = false;
                }
            }
        }

        public void Rename(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            FilePath = Path.GetFullPath(filePath);
            DisplayName = Path.GetFileName(FilePath);
            if(markdownDocument?.IsActive == true)
                markdownDocument.UpdateFilePath(FilePath);
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

        private void OnMarkdownDocumentChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(!suppressMarkdownChanges &&
               markdownDocument?.IsActive == true &&
               args.PropertyName == nameof(IMarkdownDocumentState.Text))
            {
                MarkDirty();
            }
        }

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
    }
}

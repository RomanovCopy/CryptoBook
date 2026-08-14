using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;
using System.Security.Cryptography;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    /// <summary>
    /// Loads the previous version stored next to the active document as
    /// &lt;document&gt;.bak. The backup is opened as a dirty document so the user
    /// can inspect it before deciding whether to save it over the current file.
    /// </summary>
    public sealed class DocumentBackupRecoveryService:
        IDocumentBackupRecoveryService
    {
        private readonly IDocumentSession documentSession;
        private readonly IRichTextBoxService richTextBox;
        private readonly IFlowDocumentLoadService loadService;
        private readonly IFileTemplateRegistry templateRegistry;
        private readonly IBookmarkService bookmarkService;
        private readonly ISecureFileValidator secureFileValidator;
        private readonly ISecureFileProcessor secureFileProcessor;
        private readonly IEncryptionKeyRequestService keyRequestService;

        public DocumentBackupRecoveryService(
            IDocumentSession documentSession,
            IRichTextBoxService richTextBox,
            IFlowDocumentLoadService loadService,
            IFileTemplateRegistry templateRegistry,
            IBookmarkService bookmarkService,
            ISecureFileValidator secureFileValidator,
            ISecureFileProcessor secureFileProcessor,
            IEncryptionKeyRequestService keyRequestService)
        {
            this.documentSession = documentSession ??
                throw new ArgumentNullException(nameof(documentSession));
            this.richTextBox = richTextBox ??
                throw new ArgumentNullException(nameof(richTextBox));
            this.loadService = loadService ??
                throw new ArgumentNullException(nameof(loadService));
            this.templateRegistry = templateRegistry ??
                throw new ArgumentNullException(nameof(templateRegistry));
            this.bookmarkService = bookmarkService ??
                throw new ArgumentNullException(nameof(bookmarkService));
            this.secureFileValidator = secureFileValidator ??
                throw new ArgumentNullException(nameof(secureFileValidator));
            this.secureFileProcessor = secureFileProcessor ??
                throw new ArgumentNullException(nameof(secureFileProcessor));
            this.keyRequestService = keyRequestService ??
                throw new ArgumentNullException(nameof(keyRequestService));
        }

        public string? GetBackupPath()
        {
            string? filePath = documentSession.FilePath;
            if(string.IsNullOrWhiteSpace(filePath))
                return null;

            string backupPath = filePath + ".bak";
            return File.Exists(backupPath) ? backupPath : null;
        }

        public async Task<bool> RestoreAsync(
            CancellationToken cancellationToken = default)
        {
            string? sourcePath = documentSession.FilePath;
            string? backupPath = GetBackupPath();
            if(string.IsNullOrWhiteSpace(sourcePath) || backupPath is null)
                return false;

            bool encrypted = await secureFileValidator
                .HasCryptoBookHeaderAsync(backupPath, cancellationToken);
            FlowDocument recoveredDocument;
            IFileTemplate contentTemplate;

            if(encrypted)
            {
                if(!keyRequestService.EnsureKeyAvailable())
                    return false;

                await using DecryptedFileContent decrypted =
                    await secureFileProcessor.DecryptFileContentAsync(
                        backupPath,
                        cancellationToken: cancellationToken);
                contentTemplate = FindDocumentTemplate(
                    decrypted.OriginalExtension);
                recoveredDocument = await loadService.PrepareAsync(
                    decrypted.Content,
                    contentTemplate,
                    cancellationToken);
            }
            else
            {
                contentTemplate = documentSession.Template is not null and
                    not SecureFileTemplate
                    ? documentSession.Template
                    : FindDocumentTemplate(Path.GetExtension(sourcePath));
                await using FileStream source = new(
                    backupPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    useAsync: true);
                recoveredDocument = await loadService.PrepareAsync(
                    source,
                    contentTemplate,
                    cancellationToken);
            }

            IFileTemplate sessionTemplate = encrypted
                ? templateRegistry.GetAll().First(template =>
                    template is SecureFileTemplate)
                : contentTemplate;
            documentSession.Open(
                sourcePath,
                sessionTemplate,
                recoveredDocument);
            bookmarkService.RebuildIndexFromDocument(richTextBox);
            documentSession.MarkSaved(sourcePath, sessionTemplate);
            documentSession.MarkDirty();
            return true;
        }

        public async Task SynchronizeAfterEncryptedSaveAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            string backupPath = Path.GetFullPath(filePath) + ".bak";
            if(!File.Exists(backupPath))
                return;

            try
            {
                await using DecryptedFileContent content =
                    await secureFileProcessor.DecryptFileContentAsync(
                        backupPath,
                        cancellationToken: cancellationToken);
            }
            catch(Exception exception) when(
                exception is CryptographicException or InvalidDataException)
            {
                // The backup was encrypted with another key (or cannot be
                // authenticated anymore). Publish the newly saved encrypted
                // file as a compatible backup instead of leaving an unusable
                // sidecar encrypted with the old key.
                await ReplaceWithCurrentFileAsync(
                    filePath,
                    backupPath,
                    cancellationToken);
            }
        }

        public Task SynchronizeAfterRenameAsync(
            string oldFilePath,
            string newFilePath,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(oldFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(newFilePath);
            cancellationToken.ThrowIfCancellationRequested();

            string oldBackupPath = Path.GetFullPath(oldFilePath) + ".bak";
            if(!File.Exists(oldBackupPath))
                return Task.CompletedTask;

            string newBackupPath = Path.GetFullPath(newFilePath) + ".bak";
            File.Move(oldBackupPath, newBackupPath, overwrite: true);
            return Task.CompletedTask;
        }

        private static async Task ReplaceWithCurrentFileAsync(
            string filePath,
            string backupPath,
            CancellationToken cancellationToken)
        {
            string temporaryPath = backupPath + "." +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using(FileStream source = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    useAsync: true))
                await using(FileStream destination = new(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true))
                {
                    await source.CopyToAsync(destination, cancellationToken);
                    await destination.FlushAsync(cancellationToken);
                    destination.Flush(flushToDisk: true);
                }

                AtomicFileCommit.CommitWithoutBackup(
                    temporaryPath,
                    backupPath);
            }
            finally
            {
                if(File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private IFileTemplate FindDocumentTemplate(string extension) =>
            templateRegistry.GetAll().FirstOrDefault(template =>
                template.OpenMode == FileOpenMode.Document &&
                template is not SecureFileTemplate &&
                template.CanHandleExtension(extension))
            ?? throw new NotSupportedException(
                $"Backup content format '{extension}' is not supported.");
    }
}

using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;

namespace CryptoBook.Services
{
    /// <summary>
    /// Запрашивает осознанное решение перед сменой режима сохранения
    /// и перед каждой записью защищённого документа.
    /// </summary>
    public sealed class DocumentSaveEncryptionPolicy:
        IDocumentSaveEncryptionPolicy
    {
        private readonly IMessageService messageService;
        private readonly IKeyProvider keyProvider;
        private readonly IFileTemplateRegistry templateRegistry;
        private readonly IEncryptionKeyRequestService? keyRequestService;

        public DocumentSaveEncryptionPolicy(
            IMessageService messageService,
            IKeyProvider keyProvider,
            IFileTemplateRegistry templateRegistry,
            IEncryptionKeyRequestService? keyRequestService = null)
        {
            this.messageService = messageService
                ?? throw new ArgumentNullException(nameof(messageService));
            this.keyProvider = keyProvider
                ?? throw new ArgumentNullException(nameof(keyProvider));
            this.templateRegistry = templateRegistry
                ?? throw new ArgumentNullException(nameof(templateRegistry));
            this.keyRequestService = keyRequestService;
        }

        public async Task<DocumentSaveTarget?> ResolveAsync(
            DocumentSaveTarget target,
            bool sourceIsPlaintextFile)
        {
            ArgumentNullException.ThrowIfNull(target);

            if(target.Template is SecureFileTemplate)
            {
                return await ConfirmAsync("Document.EncryptionSaveWarning") &&
                    keyRequestService?.EnsureEncryptionKeyAvailable() != false
                    ? target
                    : null;
            }

            if(!sourceIsPlaintextFile || !keyProvider.HasKey)
                return target;

            if(!await ConfirmAsync("Document.EncryptionSaveOffer"))
                return target;
            if(keyRequestService?.EnsureEncryptionKeyAvailable() == false)
                return null;

            IFileTemplate secureTemplate = templateRegistry
                .GetAll()
                .OfType<SecureFileTemplate>()
                .SingleOrDefault()
                ?? throw new InvalidOperationException(
                    LocalizationManager.GetString(
                        "Document.EncryptionFormatUnavailable"));

            string fullSourcePath = Path.GetFullPath(target.FilePath);
            string destinationDirectory = Path.GetDirectoryName(
                fullSourcePath) ?? throw new IOException(
                    LocalizationManager.Format(
                        "Security.DestinationDirectoryUnknown",
                        fullSourcePath));
            string protectedCopyPath = ProtectedCopyPathResolver
                .GetAvailablePath(
                    fullSourcePath,
                    destinationDirectory);

            return new DocumentSaveTarget(
                protectedCopyPath,
                secureTemplate);
        }

        private async Task<bool> ConfirmAsync(string messageResourceKey)
        {
            Guid dialogId = await messageService.ShowMessage(
                LocalizationManager.GetString(
                    "Document.EncryptionSaveTitle"),
                LocalizationManager.GetString(messageResourceKey),
                isCanceled: true);
            return messageService.ShowConfirmation(dialogId);
        }
    }
}

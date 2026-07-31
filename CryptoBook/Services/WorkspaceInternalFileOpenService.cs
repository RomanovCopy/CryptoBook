using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class WorkspaceInternalFileOpenService:
        IWorkspaceInternalFileOpenService
    {
        private readonly IProgressDialogService progressDialogService;
        private readonly IFlowDocumentLoadService flowDocumentLoadService;
        private readonly IRichTextBoxService richTextBoxService;
        private readonly IDocumentSession documentSession;
        private readonly IFileTemplateRegistry fileTemplateRegistry;

        public WorkspaceInternalFileOpenService(
            IProgressDialogService progressDialogService,
            IFlowDocumentLoadService flowDocumentLoadService,
            IRichTextBoxService richTextBoxService,
            IDocumentSession documentSession,
            IFileTemplateRegistry fileTemplateRegistry)
        {
            this.progressDialogService = progressDialogService ??
                throw new ArgumentNullException(nameof(progressDialogService));
            this.flowDocumentLoadService = flowDocumentLoadService ??
                throw new ArgumentNullException(nameof(flowDocumentLoadService));
            this.richTextBoxService = richTextBoxService ??
                throw new ArgumentNullException(nameof(richTextBoxService));
            this.documentSession = documentSession ??
                throw new ArgumentNullException(nameof(documentSession));
            this.fileTemplateRegistry = fileTemplateRegistry ??
                throw new ArgumentNullException(nameof(fileTemplateRegistry));
        }

        public async Task OpenDocumentAsync(
            string encryptedPath,
            string decryptedPath,
            IFileTemplate contentTemplate,
            CancellationToken cancellationToken = default)
        {
            await progressDialogService.RunAsync(
                "Открытие файла",
                async (progress, dialogToken) =>
                {
                    using var linkedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken,
                            dialogToken);
                    await using FileStream stream = new(
                        decryptedPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        81920,
                        useAsync: true);
                    await flowDocumentLoadService.LoadAsync(
                        richTextBoxService,
                        stream,
                        contentTemplate,
                        linkedTokenSource.Token,
                        progress);
                    return true;
                });

            IFileTemplate secureTemplate = fileTemplateRegistry.GetAll()
                .First(template => template is SecureFileTemplate);
            documentSession.Open(encryptedPath, secureTemplate);
        }
    }
}

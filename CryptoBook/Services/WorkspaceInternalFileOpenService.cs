using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;
using System.Windows.Documents;

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
        private readonly IBookmarkService bookmarkService;

        public WorkspaceInternalFileOpenService(
            IProgressDialogService progressDialogService,
            IFlowDocumentLoadService flowDocumentLoadService,
            IRichTextBoxService richTextBoxService,
            IDocumentSession documentSession,
            IFileTemplateRegistry fileTemplateRegistry,
            IBookmarkService bookmarkService)
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
            this.bookmarkService = bookmarkService ??
                throw new ArgumentNullException(nameof(bookmarkService));
        }

        public async Task OpenDocumentAsync(
            string sourcePath,
            string contentPath,
            IFileTemplate contentTemplate,
            bool sourceIsEncrypted,
            CancellationToken cancellationToken = default)
        {
            FlowDocument preparedDocument = await progressDialogService.RunAsync(
                LocalizationManager.GetString("Explorer.OpenFileTitle"),
                async (progress, dialogToken) =>
                {
                    using var linkedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken,
                            dialogToken);
                    await using FileStream stream = new(
                        contentPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        81920,
                        useAsync: true);
                    return await flowDocumentLoadService.PrepareAsync(
                        stream,
                        contentTemplate,
                        linkedTokenSource.Token,
                        progress);
                });

            IFileTemplate sessionTemplate = sourceIsEncrypted
                ? fileTemplateRegistry.GetAll()
                    .First(template => template is SecureFileTemplate)
                : contentTemplate;
            // Только полностью подготовленный FlowDocument заменяет активный.
            // Ошибка чтения или разбора выше оставляет редактор и сессию прежними.
            documentSession.Open(
                sourcePath,
                sessionTemplate,
                preparedDocument);
            bookmarkService.RebuildIndexFromDocument(richTextBoxService);
            // Восстановление метаданных старых закладок может изменить Tag
            // элементов и породить TextChanged. Открытый файл всё равно
            // соответствует содержимому на диске и должен остаться чистым.
            documentSession.MarkSaved(sourcePath, sessionTemplate);
        }
    }
}

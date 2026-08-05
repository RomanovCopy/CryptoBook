using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    /// <summary>
    /// Изолирует подтверждение и файловую операцию удаления от
    /// модели страницы поиска.
    /// </summary>
    public sealed class WorkspaceDocumentDeleteService:
        IWorkspaceDocumentDeleteService
    {
        private readonly IFileManagerService fileManagerService;
        private readonly IMessageService messageService;

        public WorkspaceDocumentDeleteService(
            IFileManagerService fileManagerService,
            IMessageService messageService)
        {
            this.fileManagerService = fileManagerService ??
                throw new ArgumentNullException(nameof(fileManagerService));
            this.messageService = messageService ??
                throw new ArgumentNullException(nameof(messageService));
        }

        public async Task<WorkspaceDocumentDeleteResult> DeleteAsync(
            WorkspaceContentSearchResult document,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            Guid dialogId = await messageService.ShowMessage(
                LocalizationManager.GetString(
                    "Workspace.ContentSearch.DeleteTitle"),
                LocalizationManager.Format(
                    "Workspace.ContentSearch.DeletePrompt",
                    document.Name),
                isCanceled: true);
            if(!messageService.ShowConfirmation(dialogId))
                return WorkspaceDocumentDeleteResult.Cancel();

            FileOperationResult result = await fileManagerService.DeleteAsync(
                document.FullPath,
                cancellationToken);
            return result.Success
                ? WorkspaceDocumentDeleteResult.Success()
                : WorkspaceDocumentDeleteResult.Fail(
                    result.ErrorMessage ?? LocalizationManager.GetString(
                        "Workspace.ContentSearch.DeleteFailed"));
        }
    }
}

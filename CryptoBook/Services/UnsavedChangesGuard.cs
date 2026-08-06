using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class UnsavedChangesGuard: IUnsavedChangesGuard
    {
        private readonly IDocumentSession documentSession;
        private readonly ICurrentDocumentSaver documentSaver;
        private readonly IDocumentDialogService dialogService;

        public UnsavedChangesGuard(
            IDocumentSession documentSession,
            ICurrentDocumentSaver documentSaver,
            IDocumentDialogService dialogService)
        {
            this.documentSession = documentSession ??
                throw new ArgumentNullException(nameof(documentSession));
            this.documentSaver = documentSaver ??
                throw new ArgumentNullException(nameof(documentSaver));
            this.dialogService = dialogService ??
                throw new ArgumentNullException(nameof(dialogService));
        }

        public async Task<bool> CanProceedAsync(
            CancellationToken cancellationToken = default) =>
            await CanProceedAsync(
                dialogService.ConfirmSwitchWithUnsavedChanges,
                cancellationToken);

        public async Task<bool> CanCloseAsync(
            CancellationToken cancellationToken = default) =>
            await CanProceedAsync(
                dialogService.ConfirmCloseWithUnsavedChanges,
                cancellationToken);

        private async Task<bool> CanProceedAsync(
            Func<UnsavedChangesChoice> requestChoice,
            CancellationToken cancellationToken = default)
        {
            if(!documentSession.IsDirty)
                return true;

            UnsavedChangesChoice choice = requestChoice();
            if(choice == UnsavedChangesChoice.Cancel)
                return false;
            if(choice == UnsavedChangesChoice.Discard)
                return true;

            bool saved = await documentSaver.TrySaveCurrentAsync(
                cancellationToken);
            // Во время асинхронного сохранения пользователь мог успеть
            // внести следующую правку, поэтому одного true недостаточно.
            return saved && !documentSession.IsDirty;
        }
    }
}

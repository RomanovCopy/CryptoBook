using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    /// <summary>
    /// Сохраняет аварийную копию при запуске и координирует безопасное закрытие документа,
    /// включая сохранение изменений и удаление аварийного снимка.
    /// </summary>
    public sealed class DocumentCloseCoordinator: IService
    {
        private readonly IDocumentSession documentSession;
        private readonly IUnsavedChangesGuard unsavedChangesGuard;
        private readonly IDocumentRecoveryService recoveryService;
        private readonly IDocumentDialogService dialogService;
        private bool closeInProgress;

        public DocumentCloseCoordinator(
            IDocumentSession documentSession,
            ICurrentDocumentSaver documentSaver,
            IDocumentRecoveryService recoveryService,
            IDocumentDialogService dialogService)
            : this(
                documentSession,
                new UnsavedChangesGuard(
                    documentSession,
                    documentSaver,
                    dialogService),
                recoveryService,
                dialogService)
        {
        }

        private DocumentCloseCoordinator(
            IDocumentSession documentSession,
            IUnsavedChangesGuard unsavedChangesGuard,
            IDocumentRecoveryService recoveryService,
            IDocumentDialogService dialogService)
        {
            this.documentSession = documentSession
                ?? throw new ArgumentNullException(nameof(documentSession));
            this.unsavedChangesGuard = unsavedChangesGuard
                ?? throw new ArgumentNullException(nameof(unsavedChangesGuard));
            this.recoveryService = recoveryService
                ?? throw new ArgumentNullException(nameof(recoveryService));
            this.dialogService = dialogService
                ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public bool IsCloseApproved { get; private set; }

        public Task<bool> TryApproveDocumentReplacementAsync() => unsavedChangesGuard.CanCloseAsync();

        public async Task InitializeAsync()
        {
            if(recoveryService.HasSnapshot)
            {
                // Keep the previous session's copy without prompting or letting
                // the current session overwrite it during autosave.
                await TryDeferSnapshotAsync();
            }

            recoveryService.Start();
        }

        public async Task<bool> TryApproveCloseAsync()
        {
            if(IsCloseApproved)
                return true;
            if(closeInProgress)
                return false;

            // Повторное событие Closing возможно, пока открыто модальное окно
            // или выполняется сохранение. Одновременно разрешена одна попытка.
            closeInProgress = true;
            try
            {
                if(!await unsavedChangesGuard.CanCloseAsync())
                    return false;

                await recoveryService.StopAsync();
                await TryDeleteSnapshotAsync();
                IsCloseApproved = true;
                return true;
            }
            finally
            {
                closeInProgress = false;
            }
        }

        private async Task TryDeleteSnapshotAsync()
        {
            try
            {
                await recoveryService.DeleteSnapshotAsync();
            }
            catch(Exception exception)
            {
                dialogService.ShowRecoveryCleanupError(exception);
            }
        }

        private async Task TryDeferSnapshotAsync()
        {
            try { await recoveryService.DeferSnapshotAsync(); }
            catch(Exception exception) { dialogService.ShowRecoveryCleanupError(exception); }
        }
    }
}

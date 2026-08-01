using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    /// <summary>
    /// Координирует восстановление при запуске и безопасное закрытие документа,
    /// включая сохранение изменений и удаление аварийного снимка.
    /// </summary>
    public sealed class DocumentCloseCoordinator
    {
        private readonly IDocumentSession documentSession;
        private readonly ICurrentDocumentSaver documentSaver;
        private readonly IDocumentRecoveryService recoveryService;
        private readonly IDocumentDialogService dialogService;
        private bool closeInProgress;

        public DocumentCloseCoordinator(
            IDocumentSession documentSession,
            ICurrentDocumentSaver documentSaver,
            IDocumentRecoveryService recoveryService,
            IDocumentDialogService dialogService)
        {
            this.documentSession = documentSession
                ?? throw new ArgumentNullException(nameof(documentSession));
            this.documentSaver = documentSaver
                ?? throw new ArgumentNullException(nameof(documentSaver));
            this.recoveryService = recoveryService
                ?? throw new ArgumentNullException(nameof(recoveryService));
            this.dialogService = dialogService
                ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public bool IsCloseApproved { get; private set; }

        public async Task InitializeAsync()
        {
            if(recoveryService.HasSnapshot)
            {
                if(dialogService.ConfirmRecovery())
                {
                    try
                    {
                        await recoveryService.RestoreSnapshotAsync();
                    }
                    catch(Exception exception)
                    {
                        dialogService.ShowRecoveryError(exception);
                    }
                }
                else
                {
                    await TryDeleteSnapshotAsync();
                }
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
                if(documentSession.IsDirty)
                {
                    UnsavedChangesChoice choice =
                        dialogService.ConfirmCloseWithUnsavedChanges();
                    if(choice == UnsavedChangesChoice.Cancel)
                        return false;

                    if(choice == UnsavedChangesChoice.Save)
                    {
                        bool saved =
                            await documentSaver.TrySaveCurrentAsync();
                        // Успешный вызов ещё не гарантирует чистое состояние:
                        // документ мог измениться во время асинхронного сохранения.
                        if(!saved || documentSession.IsDirty)
                            return false;
                    }
                }

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
    }
}

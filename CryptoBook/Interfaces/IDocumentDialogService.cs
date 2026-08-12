namespace CryptoBook.Interfaces
{
    public enum UnsavedChangesChoice
    {
        Save,
        Discard,
        Cancel
    }

    public interface IDocumentDialogService: IService
    {
        bool ConfirmRecovery();
        bool ConfirmBackupRecovery(string backupPath) => ConfirmRecovery();
        UnsavedChangesChoice ConfirmCloseWithUnsavedChanges();
        UnsavedChangesChoice ConfirmSwitchWithUnsavedChanges() =>
            ConfirmCloseWithUnsavedChanges();
        void ShowRecoveryError(Exception exception);
        void ShowRecoveryCleanupError(Exception exception);
    }
}

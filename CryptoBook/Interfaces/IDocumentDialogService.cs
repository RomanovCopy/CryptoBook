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
        UnsavedChangesChoice ConfirmCloseWithUnsavedChanges();
        void ShowRecoveryError(Exception exception);
        void ShowRecoveryCleanupError(Exception exception);
    }
}

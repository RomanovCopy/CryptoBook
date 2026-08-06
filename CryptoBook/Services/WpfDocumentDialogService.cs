using CryptoBook.Interfaces;

using CryptoBook.Infrastructure;

using System.Windows;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace CryptoBook.Services
{
    public sealed class WpfDocumentDialogService: IDocumentDialogService
    {
        public bool ConfirmRecovery() =>
            Show(
                LocalizationManager.GetString("Document.RecoveryPrompt"),
                LocalizationManager.GetString("Document.RecoveryTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;

        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() =>
            ConfirmUnsavedChanges("Document.UnsavedPrompt");

        public UnsavedChangesChoice ConfirmSwitchWithUnsavedChanges() =>
            ConfirmUnsavedChanges("Document.UnsavedSwitchPrompt");

        private static UnsavedChangesChoice ConfirmUnsavedChanges(
            string resourceKey) =>
            Show(
                LocalizationManager.GetString(resourceKey),
                "CryptoBook",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning) switch
            {
                MessageBoxResult.Yes => UnsavedChangesChoice.Save,
                MessageBoxResult.No => UnsavedChangesChoice.Discard,
                _ => UnsavedChangesChoice.Cancel
            };

        public void ShowRecoveryError(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            Show(
                LocalizationManager.Format(
                    "Document.RecoveryFailed",
                    Environment.NewLine,
                    exception.Message),
                LocalizationManager.GetString(
                    "Document.RecoveryErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        public void ShowRecoveryCleanupError(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            Show(
                LocalizationManager.Format(
                    "Document.RecoveryCleanupFailed",
                    Environment.NewLine,
                    exception.Message),
                LocalizationManager.GetString(
                    "Document.RecoveryCleanupErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private static MessageBoxResult Show(
            string message,
            string caption,
            MessageBoxButton buttons,
            MessageBoxImage image)
        {
            Window? owner = WpfApplication.Current?.MainWindow;
            return owner is null
                ? WpfMessageBox.Show(message, caption, buttons, image)
                : WpfMessageBox.Show(owner, message, caption, buttons, image);
        }
    }
}

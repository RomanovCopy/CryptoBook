using CryptoBook.Interfaces;

using System.Windows;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace CryptoBook.Services
{
    public sealed class WpfDocumentDialogService: IDocumentDialogService
    {
        public bool ConfirmRecovery() =>
            Show(
                "Обнаружена автоматически сохранённая копия после " +
                "предыдущего завершения. Восстановить документ?",
                "Восстановление CryptoBook",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;

        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() =>
            Show(
                "В документе есть несохранённые изменения. " +
                "Сохранить их перед закрытием?",
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
                "Не удалось восстановить документ:\n" +
                exception.Message,
                "Ошибка восстановления",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        public void ShowRecoveryCleanupError(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            Show(
                "Не удалось удалить файл автоматического восстановления. " +
                "Он может быть снова предложен при следующем запуске:\n" +
                exception.Message,
                "Ошибка очистки восстановления",
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

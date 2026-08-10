using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace CryptoBook.Views
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml.
    /// </summary>
    public partial class MainWindow: Window
    {
        private readonly DocumentCloseCoordinator closeCoordinator;
        private readonly IKeyResetService? keyResetService;
        private readonly ILockSnapshotService? snapshotService;
        private readonly IRichTextBoxService? richTextBox;
        private readonly IApplicationActivationService? activationService;
        private bool unlockDialogOpen;

        public MainWindow(DocumentCloseCoordinator closeCoordinator)
            : this(closeCoordinator, null, null, null, null)
        {
        }

        public MainWindow(
            DocumentCloseCoordinator closeCoordinator,
            IKeyResetService? keyResetService,
            ILockSnapshotService? snapshotService,
            IRichTextBoxService? richTextBox,
            IApplicationActivationService? activationService = null)
        {
            this.closeCoordinator = closeCoordinator
                ?? throw new ArgumentNullException(nameof(closeCoordinator));
            this.keyResetService = keyResetService;
            this.snapshotService = snapshotService;
            this.richTextBox = richTextBox;
            this.activationService = activationService;

            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosing;
            Closed += OnClosed;
            PreviewKeyDown += OnUserActivity;
            PreviewMouseDown += OnUserActivity;
            PreviewMouseMove += OnUserActivity;
            PreviewMouseWheel += OnUserActivity;
            InputManager.Current.PreProcessInput += OnApplicationInput;
            if(richTextBox is not null)
                richTextBox.Service.TextChanged += OnDocumentTextChanged;
            if(keyResetService is not null)
            {
                keyResetService.StateChanged += OnKeyResetStateChanged;
                keyResetService.SnapshotFailed += OnSnapshotFailed;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs args)
        {
            Loaded -= OnLoaded;
            await closeCoordinator.InitializeAsync();
            keyResetService?.Start();
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            WpfApplication.Current.SessionEnding += OnSessionEnding;
            if(activationService is not null && DataContext is IWindowWithId viewModel)
                activationService.NotifyMainWindowReady(viewModel.WindowId);
        }

        private void OnUserActivity(object sender, InputEventArgs args) =>
            keyResetService?.NotifyActivity();

        private void OnDocumentTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs args) =>
            keyResetService?.NotifyActivity();

        private void OnApplicationInput(object sender, PreProcessInputEventArgs args)
        {
            if(args.StagingItem.Input is WpfKeyEventArgs or WpfMouseEventArgs)
                keyResetService?.NotifyActivity();
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
        {
            if(args.Reason is SessionSwitchReason.SessionLock or
               SessionSwitchReason.SessionLogoff or
               SessionSwitchReason.RemoteDisconnect)
            {
                BeginSecureReset();
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
        {
            if(args.Mode == PowerModes.Suspend)
                BeginSecureReset();
        }

        private void OnSessionEnding(object sender, SessionEndingCancelEventArgs args) =>
            BeginSecureReset();

        private void BeginSecureReset()
        {
            if(keyResetService is null)
                return;
            _ = Dispatcher.BeginInvoke(new Action(async () =>
                await keyResetService.ResetAsync()));
        }

        private void OnKeyResetStateChanged(object? sender, KeyResetStateChangedEventArgs args)
        {
            if(args.State == KeyResetState.KeyReset && IsLoaded && !unlockDialogOpen)
                _ = Dispatcher.BeginInvoke(new Action(ShowUnlockDialog));
        }

        private void OnSnapshotFailed(object? sender, Exception exception)
        {
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                using IDisposable? pause = keyResetService?.Pause();
                WpfMessageBox.Show(
                    this,
                    "Не удалось создать и проверить защищённый снимок. Ключ и документ не были очищены.",
                    "Сброс ключа",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }));
        }

        private async void ShowUnlockDialog()
        {
            if(keyResetService is null || unlockDialogOpen || !IsVisible)
                return;
            unlockDialogOpen = true;
            try
            {
                var unlock = new UnlockWindow(keyResetService) { Owner = this };
                if(unlock.ShowDialog() != true || snapshotService is null || !snapshotService.Exists)
                    return;

                (_, LockSnapshotMetadata metadata) = await snapshotService.ReadAndVerifyAsync();
                bool originalAvailable = !string.IsNullOrWhiteSpace(metadata.OriginalPath) &&
                    File.Exists(metadata.OriginalPath);
                if(originalAvailable)
                {
                    var choiceWindow = new LockRecoveryChoiceWindow(
                        metadata.DocumentName,
                        metadata.OriginalPath,
                        originalAvailable)
                    {
                        Owner = this
                    };
                    choiceWindow.ShowDialog();
                    if(choiceWindow.Choice == LockRecoveryChoice.Open)
                        await keyResetService.RestoreSnapshotAsync(restoreAsUnsaved: false);
                    else if(choiceWindow.Choice == LockRecoveryChoice.Restore)
                        await keyResetService.RestoreSnapshotAsync(restoreAsUnsaved: true);
                }
                else
                {
                    var choiceWindow = new LockRecoveryChoiceWindow(
                        metadata.DocumentName,
                        metadata.OriginalPath,
                        originalAvailable)
                    {
                        Owner = this
                    };
                    choiceWindow.ShowDialog();
                    if(choiceWindow.Choice == LockRecoveryChoice.Restore)
                        await keyResetService.RestoreSnapshotAsync(restoreAsUnsaved: true);
                }
            }
            catch(Exception)
            {
                WpfMessageBox.Show(this, "Не удалось восстановить документ.", "Восстановление документа",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                unlockDialogOpen = false;
            }
        }

        private void OnClosed(object? sender, EventArgs args)
        {
            keyResetService?.Stop();
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            WpfApplication.Current.SessionEnding -= OnSessionEnding;
            if(richTextBox is not null)
                richTextBox.Service.TextChanged -= OnDocumentTextChanged;
            InputManager.Current.PreProcessInput -= OnApplicationInput;
            if(keyResetService is not null)
            {
                keyResetService.StateChanged -= OnKeyResetStateChanged;
                keyResetService.SnapshotFailed -= OnSnapshotFailed;
            }
        }

        private async void OnClosing(object? sender, CancelEventArgs args)
        {
            if(closeCoordinator.IsCloseApproved)
                return;

            args.Cancel = true;
            if(await closeCoordinator.TryApproveCloseAsync())
            {
                _ = Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(Close));
            }
        }
    }
}

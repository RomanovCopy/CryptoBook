using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Views;

using Microsoft.Win32;
using Microsoft.Xaml.Behaviors;

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using WpfApplication = System.Windows.Application;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace CryptoBook.Behaviors
{
    public sealed class WindowBehavior: Behavior<Window>
    {
        public static readonly DependencyProperty CloseSidePanelCommandProperty =
            DependencyProperty.Register(
                nameof(CloseSidePanelCommand),
                typeof(ICommand),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SidePanelProperty =
            DependencyProperty.Register(
                nameof(SidePanel),
                typeof(FrameworkElement),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty CloseCoordinatorProperty =
            DependencyProperty.Register(
                nameof(CloseCoordinator),
                typeof(DocumentCloseCoordinator),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty KeyResetServiceProperty =
            DependencyProperty.Register(
                nameof(KeyResetService),
                typeof(IKeyResetService),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SnapshotServiceProperty =
            DependencyProperty.Register(
                nameof(SnapshotService),
                typeof(ILockSnapshotService),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty RichTextBoxServiceProperty =
            DependencyProperty.Register(
                nameof(RichTextBoxService),
                typeof(IRichTextBoxService),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ActivationServiceProperty =
            DependencyProperty.Register(
                nameof(ActivationService),
                typeof(IApplicationActivationService),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        private bool unlockDialogOpen;
        private bool serviceEventsAttached;
        private bool systemEventsAttached;

        public ICommand? CloseSidePanelCommand
        {
            get => (ICommand?)GetValue(CloseSidePanelCommandProperty);
            set => SetValue(CloseSidePanelCommandProperty, value);
        }

        public FrameworkElement? SidePanel
        {
            get => (FrameworkElement?)GetValue(SidePanelProperty);
            set => SetValue(SidePanelProperty, value);
        }

        public DocumentCloseCoordinator? CloseCoordinator
        {
            get => (DocumentCloseCoordinator?)GetValue(CloseCoordinatorProperty);
            set => SetValue(CloseCoordinatorProperty, value);
        }

        public IKeyResetService? KeyResetService
        {
            get => (IKeyResetService?)GetValue(KeyResetServiceProperty);
            set => SetValue(KeyResetServiceProperty, value);
        }

        public ILockSnapshotService? SnapshotService
        {
            get => (ILockSnapshotService?)GetValue(SnapshotServiceProperty);
            set => SetValue(SnapshotServiceProperty, value);
        }

        public IRichTextBoxService? RichTextBoxService
        {
            get => (IRichTextBoxService?)GetValue(RichTextBoxServiceProperty);
            set => SetValue(RichTextBoxServiceProperty, value);
        }

        public IApplicationActivationService? ActivationService
        {
            get => (IApplicationActivationService?)GetValue(ActivationServiceProperty);
            set => SetValue(ActivationServiceProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Closing += OnClosing;
            AssociatedObject.Closed += OnClosed;
            AssociatedObject.PreviewKeyDown += OnUserActivity;
            AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;
            AssociatedObject.PreviewMouseMove += OnUserActivity;
            AssociatedObject.PreviewMouseWheel += OnUserActivity;
            InputManager.Current.PreProcessInput += OnApplicationInput;
            AttachServiceEvents();
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Closing -= OnClosing;
            AssociatedObject.Closed -= OnClosed;
            AssociatedObject.PreviewKeyDown -= OnUserActivity;
            AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
            AssociatedObject.PreviewMouseMove -= OnUserActivity;
            AssociatedObject.PreviewMouseWheel -= OnUserActivity;
            Cleanup();

            base.OnDetaching();
        }

        private async void OnLoaded(object sender, RoutedEventArgs args)
        {
            AssociatedObject.Loaded -= OnLoaded;

            if(CloseCoordinator is not null)
                await CloseCoordinator.InitializeAsync();

            KeyResetService?.Start();
            AttachSystemEvents();

            if(ActivationService is not null &&
               AssociatedObject.DataContext is IWindowWithId viewModel)
            {
                ActivationService.NotifyMainWindowReady(viewModel.WindowId);
            }
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            KeyResetService?.NotifyActivity();

            if(SidePanel is null ||
               CloseSidePanelCommand?.CanExecute(args) != true)
            {
                return;
            }

            System.Windows.Point point = args.GetPosition(SidePanel);
            if(point.X > SidePanel.ActualWidth)
                CloseSidePanelCommand.Execute(args);
        }

        private void OnUserActivity(object sender, InputEventArgs args) =>
            KeyResetService?.NotifyActivity();

        private void OnDocumentTextChanged(object sender, TextChangedEventArgs args) =>
            KeyResetService?.NotifyActivity();

        private void OnApplicationInput(object sender, PreProcessInputEventArgs args)
        {
            if(args.StagingItem.Input is WpfKeyEventArgs or WpfMouseEventArgs)
                KeyResetService?.NotifyActivity();
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
            if(KeyResetService is null)
                return;

            _ = AssociatedObject.Dispatcher.BeginInvoke(new Action(async () =>
                await KeyResetService.ResetAsync()));
        }

        private void OnKeyResetStateChanged(
            object? sender,
            KeyResetStateChangedEventArgs args)
        {
            if(args.State == KeyResetState.KeyReset &&
               AssociatedObject.IsLoaded &&
               !unlockDialogOpen)
            {
                _ = AssociatedObject.Dispatcher.BeginInvoke(
                    new Action(ShowUnlockDialog));
            }
        }

        private void OnSnapshotFailed(object? sender, Exception exception)
        {
            _ = AssociatedObject.Dispatcher.BeginInvoke(new Action(() =>
            {
                using IDisposable? pause = KeyResetService?.Pause();
                WpfMessageBox.Show(
                    AssociatedObject,
                    "Не удалось создать и проверить защищённый снимок. Ключ и документ не были очищены.",
                    "Сброс ключа",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }));
        }

        private async void ShowUnlockDialog()
        {
            if(KeyResetService is null ||
               unlockDialogOpen ||
               !AssociatedObject.IsVisible)
            {
                return;
            }

            unlockDialogOpen = true;
            try
            {
                var unlock = new UnlockWindow(KeyResetService)
                {
                    Owner = AssociatedObject
                };
                if(unlock.ShowDialog() != true ||
                   SnapshotService is null ||
                   !SnapshotService.Exists)
                {
                    return;
                }

                (_, LockSnapshotMetadata metadata) =
                    await SnapshotService.ReadAndVerifyAsync();
                bool originalAvailable =
                    !string.IsNullOrWhiteSpace(metadata.OriginalPath) &&
                    File.Exists(metadata.OriginalPath);
                var choiceWindow = new LockRecoveryChoiceWindow(
                    metadata.DocumentName,
                    metadata.OriginalPath,
                    originalAvailable)
                {
                    Owner = AssociatedObject
                };
                choiceWindow.ShowDialog();

                if(choiceWindow.Choice == LockRecoveryChoice.Open)
                {
                    await KeyResetService.RestoreSnapshotAsync(
                        restoreAsUnsaved: false);
                }
                else if(choiceWindow.Choice == LockRecoveryChoice.Restore)
                {
                    await KeyResetService.RestoreSnapshotAsync(
                        restoreAsUnsaved: true);
                }
            }
            catch(Exception)
            {
                WpfMessageBox.Show(
                    AssociatedObject,
                    "Не удалось восстановить документ.",
                    "Восстановление документа",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                unlockDialogOpen = false;
            }
        }

        private async void OnClosing(object? sender, CancelEventArgs args)
        {
            if(CloseCoordinator is null || CloseCoordinator.IsCloseApproved)
                return;

            args.Cancel = true;
            if(await CloseCoordinator.TryApproveCloseAsync())
            {
                _ = AssociatedObject.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(AssociatedObject.Close));
            }
        }

        private void OnClosed(object? sender, EventArgs args) => Cleanup();

        private void AttachServiceEvents()
        {
            if(serviceEventsAttached)
                return;

            if(RichTextBoxService is not null)
            {
                RichTextBoxService.Service.TextChanged +=
                    OnDocumentTextChanged;
            }
            if(KeyResetService is not null)
            {
                KeyResetService.StateChanged += OnKeyResetStateChanged;
                KeyResetService.SnapshotFailed += OnSnapshotFailed;
            }
            serviceEventsAttached = true;
        }

        private void AttachSystemEvents()
        {
            if(systemEventsAttached)
                return;

            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            if(WpfApplication.Current is not null)
                WpfApplication.Current.SessionEnding += OnSessionEnding;
            systemEventsAttached = true;
        }

        private void Cleanup()
        {
            KeyResetService?.Stop();
            InputManager.Current.PreProcessInput -= OnApplicationInput;

            if(systemEventsAttached)
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                if(WpfApplication.Current is not null)
                {
                    WpfApplication.Current.SessionEnding -= OnSessionEnding;
                }
                systemEventsAttached = false;
            }

            if(serviceEventsAttached)
            {
                if(RichTextBoxService is not null)
                {
                    RichTextBoxService.Service.TextChanged -=
                        OnDocumentTextChanged;
                }
                if(KeyResetService is not null)
                {
                    KeyResetService.StateChanged -= OnKeyResetStateChanged;
                    KeyResetService.SnapshotFailed -= OnSnapshotFailed;
                }
                serviceEventsAttached = false;
            }
        }
    }
}

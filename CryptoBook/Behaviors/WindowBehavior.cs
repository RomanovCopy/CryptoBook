using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Views;
using CryptoBook.Infrastructure;
using Button = System.Windows.Controls.Button;

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
        private readonly List<Window> securityHiddenWindows = new();

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
            if(AssociatedObject.FindName("SecurityUnlockButton") is Button unlockButton)
                unlockButton.Click += OnUnlockClick;
            if(AssociatedObject.FindName("SecurityCloseButton") is Button closeButton)
                closeButton.Click += OnSecurityCloseClick;

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

        private void OnUserActivity(object sender, InputEventArgs args)
        {
            if(IsWorkspaceLocked && args is WpfKeyEventArgs key &&
                (Keyboard.Modifiers != ModifierKeys.None || key.Key is not (Key.Tab or Key.Enter or Key.Space or Key.Escape)))
                args.Handled = true;
            else
                KeyResetService?.NotifyActivity();
        }

        private bool IsWorkspaceLocked => WorkspaceLockPresentation.IsLocked(KeyResetService?.State,
            KeyResetService?.HasRetainedDocument == true);

        private void OnUnlockClick(object sender, RoutedEventArgs args) => ShowUnlockDialog();
        private void OnSecurityCloseClick(object sender, RoutedEventArgs args) => AssociatedObject.Close();

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
            if(AssociatedObject.FindName("SecurityWorkspace") is FrameworkElement workspace &&
               AssociatedObject.FindName("SecurityLockShield") is FrameworkElement shield)
            {
                bool locked = IsWorkspaceLocked;
                WorkspaceLockPresentation.Apply(workspace, shield,
                    AssociatedObject.FindName("SecurityUnlockButton") as Button,
                    AssociatedObject.FindName("SecurityCloseButton") as Button,
                    args.State, KeyResetService?.HasRetainedDocument == true);
                if(locked)
                {
                    foreach(Window window in WpfApplication.Current.Windows.Cast<Window>().ToArray())
                        if(window != AssociatedObject && window is not UnlockWindow && window.IsVisible)
                        {
                            if(window is MediaPlayer)
                            {
                                window.Close();
                                continue;
                            }
                            securityHiddenWindows.Add(window);
                            window.Hide();
                        }
                }
                else
                {
                    foreach(Window window in securityHiddenWindows)
                    {
                        if(args.State == KeyResetState.KeyReset && KeyResetService?.HasRetainedDocument != true)
                            window.Close();
                        else if(window.IsLoaded)
                            window.Show();
                    }
                    securityHiddenWindows.Clear();
                }
            }
        }

        private void OnSnapshotFailed(object? sender, Exception exception)
        {
            _ = AssociatedObject.Dispatcher.BeginInvoke(new Action(() =>
            {
                using IDisposable? pause = KeyResetService?.Pause();
                WpfMessageBox.Show(
                    AssociatedObject,
                    LocalizationManager.GetString("Security.SnapshotFailedLocked"),
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
                bool restoringRetainedDocument = KeyResetService.HasRetainedDocument;
                if(!restoringRetainedDocument && CloseCoordinator is not null &&
                   !await CloseCoordinator.TryApproveDocumentReplacementAsync())
                    return;
                LockSnapshotNotice? notice = SnapshotService?.GetNotice();
                var unlock = new UnlockWindow(KeyResetService,
                    restoringRetainedDocument || notice is null ? null : LockSnapshotNoticePresentation.Describe(notice))
                {
                    Owner = AssociatedObject
                };
                if(unlock.ShowDialog() != true ||
                   restoringRetainedDocument ||
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
            if(IsWorkspaceLocked && KeyResetService?.HasRetainedDocument == true)
            {
                args.Cancel = true;
                _ = AssociatedObject.Dispatcher.BeginInvoke(new Action(ShowUnlockDialog));
                return;
            }
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

        private void OnClosed(object? sender, EventArgs args)
        {
            if(AssociatedObject.FindName("SecurityUnlockButton") is Button button)
                button.Click -= OnUnlockClick;
            if(AssociatedObject.FindName("SecurityCloseButton") is Button closeButton)
                closeButton.Click -= OnSecurityCloseClick;
            securityHiddenWindows.Clear();
            Cleanup();
        }

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

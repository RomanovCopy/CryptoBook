using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class UpdateNotificationViewModel:
        ViewModelBase,
        IUpdateNotificationViewModel,
        IDisposable
    {
        private readonly IApplicationUpdateCoordinator updateCoordinator;
        private readonly IUriNavigationService uriNavigationService;
        private readonly IApplicationUpdateInstaller? updateInstaller;
        private readonly AsyncRelayCommand openReleaseCommand;
        private readonly RelayCommand remindLaterCommand;
        private readonly AsyncRelayCommand skipVersionCommand;
        private ApplicationRelease? availableRelease;
        private bool isVisible;
        private string checkStatus = string.Empty;
        private bool disposed;

        public UpdateNotificationViewModel(
            IApplicationUpdateCoordinator updateCoordinator,
            IUriNavigationService uriNavigationService,
            IApplicationUpdateInstaller? updateInstaller = null)
        {
            this.updateCoordinator = updateCoordinator ??
                throw new ArgumentNullException(nameof(updateCoordinator));
            this.uriNavigationService = uriNavigationService ??
                throw new ArgumentNullException(nameof(uriNavigationService));
            this.updateInstaller = updateInstaller;

            openReleaseCommand = new AsyncRelayCommand(
                (_, token) => InstallAvailableReleaseAsync(token),
                _ => availableRelease is not null);
            remindLaterCommand = new RelayCommand(
                _ => Hide(),
                _ => availableRelease is not null);
            skipVersionCommand = new AsyncRelayCommand(
                (_, token) => SkipAvailableVersionAsync(token),
                _ => availableRelease is not null);
            LocalizationManager.CultureChanged += OnCultureChanged;
        }

        public bool IsVisible
        {
            get => isVisible;
            private set => SetProperty(ref isVisible, value);
        }

        public string Title => availableRelease is null
            ? string.Empty
            : LocalizationManager.Format(
                "Update.Available",
                availableRelease.Version);

        public string Description => availableRelease is null
            ? string.Empty
            : LocalizationManager.GetString("Update.Description");

        public string CheckStatus
        {
            get => checkStatus;
            private set => SetProperty(ref checkStatus, value);
        }

        public ICommand OpenRelease => openReleaseCommand;
        public ICommand RemindLater => remindLaterCommand;
        public ICommand SkipVersion => skipVersionCommand;

        public async Task CheckAsync(
            CancellationToken cancellationToken = default)
            => await CheckInternalAsync(false, false, cancellationToken);

        public async Task CheckNowAsync(
            CancellationToken cancellationToken = default)
        {
            CheckStatus = LocalizationManager.GetString("Update.Checking");
            await CheckInternalAsync(true, true, cancellationToken);
        }

        private async Task CheckInternalAsync(
            bool force,
            bool reportStatus,
            CancellationToken cancellationToken)
        {
            try
            {
                ApplicationRelease? release = force
                    ? await updateCoordinator.CheckNowAsync(cancellationToken)
                    : await updateCoordinator.CheckAsync(cancellationToken);
                SetAvailableRelease(release);
                if(reportStatus)
                {
                    CheckStatus = release is null
                        ? LocalizationManager.GetString("Update.UpToDate")
                        : LocalizationManager.Format(
                            "Update.CheckFound",
                            release.Version);
                }
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                Hide();
                if(reportStatus)
                    CheckStatus = LocalizationManager.GetString("Update.CheckCanceled");
            }
            catch(TaskCanceledException)
            {
                Hide();
                if(reportStatus)
                    CheckStatus = LocalizationManager.GetString("Update.CheckFailed");
            }
            catch(HttpRequestException)
            {
                Hide();
                if(reportStatus)
                    CheckStatus = LocalizationManager.GetString("Update.CheckFailed");
            }
            catch(JsonException)
            {
                Hide();
                if(reportStatus)
                    CheckStatus = LocalizationManager.GetString("Update.CheckFailed");
            }
            catch(InvalidDataException)
            {
                Hide();
                if(reportStatus)
                    CheckStatus = LocalizationManager.GetString("Update.CheckFailed");
            }
        }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            LocalizationManager.CultureChanged -= OnCultureChanged;
        }

        private async Task InstallAvailableReleaseAsync(
            CancellationToken cancellationToken)
        {
            ApplicationRelease? release = availableRelease;
            if(release is null)
                return;

            // Releases created by older/custom sources may not expose assets yet.
            // Keep the release-page fallback for those sources while GitHub releases
            // with the Inno Setup asset use the self-updater below.
            if(updateInstaller is null || release.InstallerUri is null)
            {
                if(uriNavigationService.TryOpen(release.ReleaseUri))
                    Hide();
                return;
            }

            await updateInstaller.InstallAsync(release, cancellationToken);
            Hide();
            ShutdownApplication();
        }

        private static void ShutdownApplication()
        {
            System.Windows.Application? application =
                System.Windows.Application.Current;
            if(application is null)
                return;

            if(!application.Dispatcher.CheckAccess())
            {
                application.Dispatcher.BeginInvoke(
                    new Action(ShutdownApplication));
                return;
            }

            if(application.MainWindow is not null)
                application.Shutdown();
        }

        private async Task SkipAvailableVersionAsync(
            CancellationToken cancellationToken)
        {
            ApplicationRelease? release = availableRelease;
            if(release is null)
                return;

            await updateCoordinator.SkipAsync(release, cancellationToken);
            Hide();
        }

        private void SetAvailableRelease(ApplicationRelease? release)
        {
            availableRelease = release;
            IsVisible = release is not null;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
            openReleaseCommand.RaiseCanExecuteChanged();
            remindLaterCommand.RaiseCanExecuteChanged();
            skipVersionCommand.RaiseCanExecuteChanged();
        }

        private void Hide()
        {
            IsVisible = false;
        }

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
            if(!string.IsNullOrEmpty(CheckStatus))
                CheckStatus = string.Empty;
        }
    }
}

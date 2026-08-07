using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

using System.Net.Http;

using Xunit;

namespace CryptoBook.Tests;

public sealed class UpdateNotificationViewModelTests
{
    [Fact]
    public async Task CheckAsync_ShowsNotificationAndOpenReleaseUsesNavigationService()
    {
        ApplicationRelease release = CreateRelease("1.2.0");
        var coordinator = new StubCoordinator(release);
        var navigation = new TestUriNavigationService();
        using var viewModel = new UpdateNotificationViewModel(
            coordinator,
            navigation);

        await viewModel.CheckAsync();

        Assert.True(viewModel.IsVisible);
        Assert.Contains("1.2.0", viewModel.Title);
        Assert.True(viewModel.OpenRelease.CanExecute(null));

        viewModel.OpenRelease.Execute(null);

        Assert.False(viewModel.IsVisible);
        Assert.Equal(release.ReleaseUri, navigation.LastOpenedUri);
    }

    [Fact]
    public async Task RemindLater_HidesNotificationWithoutSkippingVersion()
    {
        var coordinator = new StubCoordinator(CreateRelease("1.2.0"));
        using var viewModel = new UpdateNotificationViewModel(
            coordinator,
            new TestUriNavigationService());
        await viewModel.CheckAsync();

        viewModel.RemindLater.Execute(null);

        Assert.False(viewModel.IsVisible);
        Assert.Null(coordinator.SkippedRelease);
    }

    [Fact]
    public async Task OpenRelease_DownloadsInstallerInsteadOfOpeningBrowser()
    {
        ApplicationRelease release = CreateRelease("1.2.0") with
        {
            InstallerUri = new Uri(
                "https://github.com/RomanovCopy/CryptoBook/releases/download/" +
                "v1.2.0/CryptoBook-Setup-1.2.0.exe")
        };
        var navigation = new TestUriNavigationService();
        var installer = new StubUpdateInstaller();
        using var viewModel = new UpdateNotificationViewModel(
            new StubCoordinator(release),
            navigation,
            installer);
        await viewModel.CheckAsync();

        await Assert.IsAssignableFrom<IAsyncCommand>(viewModel.OpenRelease)
            .ExecuteAsync();

        Assert.Same(release, installer.Release);
        Assert.Null(navigation.LastOpenedUri);
        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public async Task SkipVersion_PersistsVersionAndHidesNotification()
    {
        ApplicationRelease release = CreateRelease("1.2.0");
        var coordinator = new StubCoordinator(release);
        using var viewModel = new UpdateNotificationViewModel(
            coordinator,
            new TestUriNavigationService());
        await viewModel.CheckAsync();

        await Assert.IsAssignableFrom<IAsyncCommand>(viewModel.SkipVersion)
            .ExecuteAsync();

        Assert.False(viewModel.IsVisible);
        Assert.Same(release, coordinator.SkippedRelease);
    }

    [Fact]
    public async Task CheckAsync_SuppressesNetworkFailure()
    {
        using var viewModel = new UpdateNotificationViewModel(
            new StubCoordinator(new HttpRequestException("Offline")),
            new TestUriNavigationService());

        await viewModel.CheckAsync();

        Assert.False(viewModel.IsVisible);
    }

    private static ApplicationRelease CreateRelease(string version)
    {
        Assert.True(SemanticVersion.TryParse(version, out SemanticVersion? semanticVersion));
        return new ApplicationRelease(
            semanticVersion!,
            $"CryptoBook {version}",
            string.Empty,
            new Uri($"https://github.com/RomanovCopy/CryptoBook/releases/tag/v{version}"),
            DateTimeOffset.UtcNow);
    }

    private sealed class StubCoordinator: IApplicationUpdateCoordinator
    {
        private readonly ApplicationRelease? release;
        private readonly Exception? exception;

        public StubCoordinator(ApplicationRelease release)
        {
            this.release = release;
        }

        public StubCoordinator(Exception exception)
        {
            this.exception = exception;
        }

        public ApplicationRelease? SkippedRelease { get; private set; }

        public Task<ApplicationRelease?> CheckAsync(
            CancellationToken cancellationToken = default) =>
            exception is null
                ? Task.FromResult(release)
                : Task.FromException<ApplicationRelease?>(exception);

        public Task SkipAsync(
            ApplicationRelease release,
            CancellationToken cancellationToken = default)
        {
            SkippedRelease = release;
            return Task.CompletedTask;
        }
    }

    private sealed class StubUpdateInstaller: IApplicationUpdateInstaller
    {
        public ApplicationRelease? Release { get; private set; }

        public Task InstallAsync(
            ApplicationRelease release,
            CancellationToken cancellationToken = default)
        {
            Release = release;
            return Task.CompletedTask;
        }
    }
}

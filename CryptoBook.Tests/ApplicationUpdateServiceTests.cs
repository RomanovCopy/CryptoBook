using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ApplicationUpdateServiceTests
{
    [Fact]
    public void AssemblyVersionProvider_ReturnsCurrentFourComponentVersion()
    {
        var provider = new AssemblyApplicationVersionProvider();

        Assert.Equal("1.1.3.0", provider.GetCurrentVersion().ToString());
    }

    [Fact]
    public async Task CheckAsync_ReturnsReleaseWhenItIsNewer()
    {
        ApplicationRelease release = CreateRelease("1.1.0");
        var service = new ApplicationUpdateService(
            new StubReleaseSource(release),
            new StubVersionProvider("1.0.7"));

        ApplicationRelease? result = await service.CheckAsync();

        Assert.Same(release, result);
    }

    [Fact]
    public async Task CheckAsync_ComparesFourComponentReleaseVersions()
    {
        ApplicationRelease release = CreateRelease("1.1.0.2");
        var service = new ApplicationUpdateService(
            new StubReleaseSource(release),
            new StubVersionProvider("1.1.0.1"));

        ApplicationRelease? result = await service.CheckAsync();

        Assert.Same(release, result);
    }

    [Theory]
    [InlineData("1.0.7")]
    [InlineData("1.0.6")]
    public async Task CheckAsync_IgnoresCurrentOrOlderRelease(string latest)
    {
        var service = new ApplicationUpdateService(
            new StubReleaseSource(CreateRelease(latest)),
            new StubVersionProvider("1.0.7"));

        Assert.Null(await service.CheckAsync());
    }

    private static ApplicationRelease CreateRelease(string version) =>
        new(
            Parse(version),
            $"CryptoBook {version}",
            string.Empty,
            new Uri($"https://github.com/RomanovCopy/CryptoBook/releases/tag/v{version}"),
            DateTimeOffset.UtcNow);

    private static SemanticVersion Parse(string value)
    {
        Assert.True(SemanticVersion.TryParse(value, out SemanticVersion? version));
        return version!;
    }

    private sealed class StubReleaseSource(ApplicationRelease? release): IReleaseSource
    {
        public Task<ApplicationRelease?> GetLatestAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(release);
    }

    private sealed class StubVersionProvider(string version): IApplicationVersionProvider
    {
        public SemanticVersion GetCurrentVersion() => Parse(version);
    }
}

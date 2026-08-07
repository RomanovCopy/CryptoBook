using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ApplicationUpdateCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CheckAsync_QueriesReleaseAndRecordsCheckWhenDue()
    {
        ApplicationRelease release = CreateRelease("1.1.0");
        var updateService = new StubUpdateService(release);
        var stateStore = new MemoryStateStore(UpdateCheckState.Empty);
        var coordinator = CreateCoordinator(updateService, stateStore);

        ApplicationRelease? result = await coordinator.CheckAsync();

        Assert.Same(release, result);
        Assert.Equal(1, updateService.CheckCount);
        Assert.Equal(Now, stateStore.State.LastCheckUtc);
    }

    [Fact]
    public async Task CheckAsync_DoesNotQueryAgainWithinInterval()
    {
        var updateService = new StubUpdateService(CreateRelease("1.1.0"));
        var stateStore = new MemoryStateStore(
            new UpdateCheckState(Now.AddHours(-1), null));
        var coordinator = CreateCoordinator(updateService, stateStore);

        Assert.Null(await coordinator.CheckAsync());
        Assert.Equal(0, updateService.CheckCount);
    }

    [Fact]
    public async Task CheckAsync_IgnoresOnlyTheSkippedVersion()
    {
        var skippedService = new StubUpdateService(CreateRelease("1.1.0"));
        var skippedStore = new MemoryStateStore(
            new UpdateCheckState(Now.AddDays(-2), "1.1.0"));

        Assert.Null(await CreateCoordinator(skippedService, skippedStore).CheckAsync());

        ApplicationRelease nextRelease = CreateRelease("1.2.0");
        var nextService = new StubUpdateService(nextRelease);
        var nextStore = new MemoryStateStore(
            new UpdateCheckState(Now.AddDays(-2), "1.1.0"));

        Assert.Same(
            nextRelease,
            await CreateCoordinator(nextService, nextStore).CheckAsync());
    }

    [Fact]
    public async Task CheckAsync_QueriesWhenStoredTimeIsInFuture()
    {
        var updateService = new StubUpdateService(CreateRelease("1.1.0"));
        var stateStore = new MemoryStateStore(
            new UpdateCheckState(Now.AddDays(1), null));

        await CreateCoordinator(updateService, stateStore).CheckAsync();

        Assert.Equal(1, updateService.CheckCount);
        Assert.Equal(Now, stateStore.State.LastCheckUtc);
    }

    [Fact]
    public async Task SkipAsync_PersistsSelectedVersion()
    {
        var store = new MemoryStateStore(UpdateCheckState.Empty);
        var coordinator = CreateCoordinator(
            new StubUpdateService(null),
            store);

        await coordinator.SkipAsync(CreateRelease("1.3.0"));

        Assert.Equal("1.3.0", store.State.SkippedVersion);
    }

    private static ApplicationUpdateCoordinator CreateCoordinator(
        IApplicationUpdateService service,
        IUpdateCheckStateStore store) =>
        new(
            service,
            store,
            new UpdateCheckOptions(TimeSpan.FromHours(24)),
            new FixedTimeProvider(Now));

    private static ApplicationRelease CreateRelease(string version) =>
        new(
            Parse(version),
            $"CryptoBook {version}",
            string.Empty,
            new Uri($"https://github.com/RomanovCopy/CryptoBook/releases/tag/v{version}"),
            Now);

    private static SemanticVersion Parse(string value)
    {
        Assert.True(SemanticVersion.TryParse(value, out SemanticVersion? version));
        return version!;
    }

    private sealed class StubUpdateService(ApplicationRelease? release):
        IApplicationUpdateService
    {
        public int CheckCount { get; private set; }

        public Task<ApplicationRelease?> CheckAsync(
            CancellationToken cancellationToken = default)
        {
            CheckCount++;
            return Task.FromResult(release);
        }
    }

    private sealed class MemoryStateStore(UpdateCheckState state):
        IUpdateCheckStateStore
    {
        public UpdateCheckState State { get; private set; } = state;

        public Task<UpdateCheckState> LoadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task SaveAsync(
            UpdateCheckState state,
            CancellationToken cancellationToken = default)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now): TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

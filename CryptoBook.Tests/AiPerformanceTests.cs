using CryptoBook.AI;
using System.IO;
using System.Text.Json;
using Xunit;

namespace CryptoBook.Tests;

public sealed class AiPerformanceTests : IDisposable
{
    private const long GiB = 1024L * 1024 * 1024;
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-Performance-" + Guid.NewGuid().ToString("N"));
    private readonly List<AiWorkerRequest> requests = [];
    private readonly List<Worker> workers = [];
    private AiResourceSnapshot resources = new(16 * GiB, 12 * GiB, 20 * GiB, true);
    private AiModelSettings Settings => new() { ModelPath = Path.Combine(directory, "model.gguf"), AllowPerformanceBoost = true };
    private AiRuntimeProfileStore Store => new(Path.Combine(directory, "profiles.json"));
    private static AiModelInspection Inspection => new(new(4, 8, 500_000_000, 200_000_000, 300_000_000,
        2 * GiB, 12 * GiB, "Test GPU"), new() { ["general.architecture"] = "llama" }, "test-hardware");
    private static AiPerformanceMeasurement Measurement(bool boost) => new(200, 64, boost ? 0.5 : 1, boost ? 1 : 2, GiB, GiB / 2);

    public AiPerformanceTests()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Settings.ModelPath, "GGUF"u8.ToArray());
    }
    private AdaptiveAiInferenceService Create(Func<AiWorkerRequest, CancellationToken, Task>? action = null,
        Func<AiWorkerRequest, AiPerformanceMeasurement>? measure = null) => new(Store, () =>
    {
        var worker = new Worker(async (request, token) =>
        {
            requests.Add(request);
            if(action is not null) await action(request, token);
        }, measure ?? (request => Measurement(request.Options.PerformanceBoost)));
        workers.Add(worker);
        return worker;
    }, () => resources);
    private Task Generate(AdaptiveAiInferenceService service, AiModelSettings settings) =>
        service.GenerateAsync(settings, [new("user", "private conversation")], _ => { }, null, CancellationToken.None);

    [Fact]
    public void ExistingSettings_DefaultToNoConsent_AndRoundTripPermissionPerModel()
    {
        var existing = JsonSerializer.Deserialize<AiModelSettings>("{\"ContextLength\":4096}")!;
        Assert.False(existing.AllowPerformanceBoost);
        Assert.Equal(AiPerformanceMode.Balanced, existing.EffectivePerformanceMode);
        Assert.Equal(AiPerformanceMode.Optimal,
            JsonSerializer.Deserialize<AiModelSettings>("{\"AllowPerformanceBoost\":true}")!.EffectivePerformanceMode);
        Assert.True(existing.BoostOnlyOnAc);
        var store = new AiSettingsStore(Path.Combine(directory, "settings.json"));
        store.Save(new() { Models = [Settings, Settings with { ModelPath = "second.gguf", AllowPerformanceBoost = false }] });
        var loaded = new AiSettingsStore(Path.Combine(directory, "settings.json")).Current;
        Assert.True(loaded.Models[0].AllowPerformanceBoost);
        Assert.False(loaded.Models[1].AllowPerformanceBoost);
    }

    [Theory]
    [InlineData(AiPerformanceMode.Balanced)]
    [InlineData(AiPerformanceMode.PowerSaving)]
    [InlineData(AiPerformanceMode.Optimal)]
    public void ExplicitMode_OverridesLegacyPermission_AndRoundTrips(AiPerformanceMode mode)
    {
        var settings = Settings with { PerformanceMode = mode, AllowPerformanceBoost = false };
        var restored = JsonSerializer.Deserialize<AiModelSettings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal(mode, restored.EffectivePerformanceMode);
        Assert.Equal(mode == AiPerformanceMode.Optimal, AiPerformancePolicy.IsAllowed(restored, resources));
        settings.Validate();
        var error = Assert.Throws<AiException>(() => (settings with { PerformanceMode = (AiPerformanceMode)999 }).Validate());
        Assert.Equal("AI.InvalidPerformanceMode", error.ResourceKey);
    }

    [Theory]
    [InlineData(AiPerformanceMode.Balanced, false, false)]
    [InlineData(AiPerformanceMode.PowerSaving, false, false)]
    [InlineData(AiPerformanceMode.Optimal, false, false)]
    [InlineData(AiPerformanceMode.Optimal, true, false)]
    [InlineData(AiPerformanceMode.Optimal, true, true)]
    public async Task ContextReset_KeepsLoadedWorkerAndProfile_WithoutRequestingInferenceBoost(
        AiPerformanceMode mode, bool loseAc, bool memoryPressure)
    {
        using var service = Create((request, _) =>
        {
            // Enforce the same preconditions as the real worker transport.
            if(request.Options.PerformanceBoost)
            {
                if(!AiPerformancePolicy.IsAllowed(request.Settings, resources)) throw new AiException("AI.PerformanceConsentRequired");
                if(!resources.HasHeadroom) throw new AiException("AI.ResourcePressure");
            }
            return Task.CompletedTask;
        });
        var settings = Settings with { PerformanceMode = mode, ReuseContext = true };
        if(mode == AiPerformanceMode.Optimal) await service.TestPerformanceAsync(settings, CancellationToken.None);
        await service.LoadAsync(settings, null, CancellationToken.None);
        var loadedWorker = workers.Last();
        var options = service.RuntimeOptions;
        Assert.Equal(mode == AiPerformanceMode.Optimal, options!.PerformanceBoost);
        int workerCount = workers.Count;
        var originalResources = resources;
        if(loseAc) resources = resources with { OnAc = false };
        if(memoryPressure) resources = resources with { AvailableRam = GiB, AvailableCommit = GiB };
        await Task.Delay(10);
        DateTime resetStarted = DateTime.UtcNow;

        await service.ResetContextAsync();

        var reset = requests.Last();
        Assert.Equal("reset", reset.Command);
        Assert.Empty(reset.Turns);
        Assert.False(reset.Options.PerformanceBoost);
        Assert.True(loadedWorker.IsAlive);
        Assert.True(service.IsLoaded);
        Assert.False(service.IsBusy);
        Assert.Same(options, service.RuntimeOptions);
        Assert.True(service.LastUsedUtc >= resetStarted);
        resources = originalResources;
        await Generate(service, settings);
        Assert.Equal(workerCount, workers.Count);
        Assert.True(loadedWorker.IsAlive);
        await service.UnloadAsync();
        Assert.False(service.IsLoaded);
        Assert.False(loadedWorker.IsAlive);
    }

    [Fact]
    public async Task ChangingModes_ReloadsWorkerAndRestoresMeasuredProfile()
    {
        using var service = Create();
        await service.TestPerformanceAsync(Settings, CancellationToken.None);
        await Generate(service, Settings);
        Assert.True(requests.Last().Options.PerformanceBoost);
        var boostedWorker = workers.Last();
        await Generate(service, Settings with { PerformanceMode = AiPerformanceMode.PowerSaving });
        Assert.False(boostedWorker.IsAlive);
        var saving = requests.Last().Options;
        Assert.True(saving.PowerSaving);
        Assert.False(saving.PerformanceBoost);
        Assert.Equal(Math.Max(1, Environment.ProcessorCount / 4), saving.Threads);
        Assert.Equal(saving.Threads, saving.BatchThreads);
        await Generate(service, Settings with { PerformanceMode = AiPerformanceMode.Balanced });
        Assert.False(requests.Last().Options.PowerSaving);
        Assert.False(requests.Last().Options.PerformanceBoost);
        Assert.Equal(saving.GpuLayers, requests.Last().Options.GpuLayers);
        Assert.Equal(saving.ContextLength, requests.Last().Options.ContextLength);
        await Generate(service, Settings);
        Assert.True(requests.Last().Options.PerformanceBoost);
    }

    [Fact]
    public async Task PowerSaving_FallbackAndCachedProfileKeepReducedParallelism()
    {
        var settings = Settings with { PerformanceMode = AiPerformanceMode.PowerSaving };
        using(var service = Create((request, _) =>
        {
            if(request.Command == "probe" && request.Options.FlashAttention is null)
                throw new AiException("AI.LoadFailed");
            return Task.CompletedTask;
        }))
            await Generate(service, settings);
        int previousCount = requests.Count;
        using(var service = Create()) await Generate(service, settings);
        Assert.Contains(requests.Skip(previousCount), request => request.Command == "probe" && request.Options.FlashAttention == false);
        Assert.All(requests.Where(request => request.Command != "inspect"), request =>
        {
            Assert.True(request.Options.PowerSaving);
            Assert.False(request.Options.PerformanceBoost);
            Assert.Equal(Math.Max(1, Environment.ProcessorCount / 4), request.Options.Threads);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(128)]
    public void PowerSaving_KeepsMemorySettingsAndReducesCpuParallelism(int processors)
    {
        var baseline = new AiRuntimeOptions(8192, 12);
        var saving = AiPerformancePolicy.ApplyMode(baseline, Settings with { PerformanceMode = AiPerformanceMode.PowerSaving }, processors);
        Assert.True(saving.IsValid);
        Assert.Equal(Math.Max(1, processors / 4), saving.Threads);
        Assert.Equal(saving.Threads, saving.BatchThreads);
        Assert.Equal(baseline, saving with { Threads = 0, BatchThreads = 0, PowerSaving = false });
        Assert.False((saving with { PerformanceBoost = true }).IsValid);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public async Task MissingConsentOrAc_DoesNotStartWorkers(bool consent, bool onlyAc, bool onAc)
    {
        resources = resources with { OnAc = onAc };
        using var service = Create();
        var error = await Assert.ThrowsAsync<AiException>(() => service.TestPerformanceAsync(
            Settings with { AllowPerformanceBoost = consent, BoostOnlyOnAc = onlyAc }, CancellationToken.None));
        Assert.Equal("AI.PerformanceConsentRequired", error.ResourceKey);
        Assert.Empty(workers);
    }

    [Fact]
    public async Task LowMemory_DoesNotStartTestOrReplaceProfile()
    {
        File.WriteAllText(Path.Combine(directory, "profiles.json"), "[]");
        resources = resources with { AvailableRam = GiB };
        using var service = Create();
        var error = await Assert.ThrowsAsync<AiException>(() => service.TestPerformanceAsync(Settings, CancellationToken.None));
        Assert.Equal("AI.ResourcePressure", error.ResourceKey);
        Assert.Empty(workers);
        Assert.Equal("[]", File.ReadAllText(Path.Combine(directory, "profiles.json")));
    }

    [Fact]
    public async Task Test_ComparesEqualContext_StoresMeasuredWinner_AndNeverSendsPrivateContent()
    {
        using var service = Create();
        var result = await service.TestPerformanceAsync(Settings with { SystemPrompt = "private instructions" }, CancellationToken.None);
        Assert.True(result.BoostSelected);
        Assert.Equal(2, result.Speedup, 5);
        Assert.All(requests, request => Assert.Empty(request.Turns));
        Assert.All(requests, request => Assert.Empty(request.Settings.SystemPrompt));
        Assert.All(requests, request => Assert.Equal(Settings.ContextLength, request.Options.ContextLength));
        Assert.All(requests, request => Assert.True(request.ProtectResources));
        Assert.InRange(requests.Count(request => request.Command == "benchmark"), 2, 4);
        Assert.All(workers, worker => Assert.False(worker.IsAlive));
        Assert.False(service.IsBusy);
        Assert.False(service.IsLoaded);
        var cached = Store.Find(AdaptiveAiInferenceService.PerformanceKey(Settings, Inspection.HardwareKey));
        Assert.True(cached!.Options.PerformanceBoost);
        Assert.True(cached.Performance!.IsValid);
        Assert.DoesNotContain("private", File.ReadAllText(Path.Combine(directory, "profiles.json")));
        Assert.Equal(result, service.GetLastPerformance(Settings));
    }

    [Fact]
    public async Task SmallGain_DoesNotEnableBoost()
    {
        using var service = Create(measure: request => Measurement(false) with
            { GenerationSeconds = request.Options.PerformanceBoost ? 1.99 : 2 });
        var result = await service.TestPerformanceAsync(Settings, CancellationToken.None);
        Assert.False(result.BoostSelected);
        Assert.Equal(result.Baseline, result.Selected);
        await Generate(service, Settings);
        Assert.False(requests.Last().Options.PerformanceBoost);
    }

    [Fact]
    public async Task PermissionAndPower_AreRecheckedWhenReusingLoadedModel()
    {
        using var service = Create();
        await service.TestPerformanceAsync(Settings, CancellationToken.None);
        await Generate(service, Settings);
        Assert.True(requests.Last().Options.PerformanceBoost);
        resources = resources with { OnAc = false };
        await Generate(service, Settings);
        Assert.False(requests.Last().Options.PerformanceBoost);
        resources = resources with { OnAc = true };
        await Generate(service, Settings);
        Assert.True(requests.Last().Options.PerformanceBoost);
        await Generate(service, Settings with { AllowPerformanceBoost = false });
        Assert.False(requests.Last().Options.PerformanceBoost);
    }

    [Fact]
    public async Task ChangedModel_InvalidatesMeasuredProfile()
    {
        using var service = Create();
        await service.TestPerformanceAsync(Settings, CancellationToken.None);
        File.AppendAllText(Settings.ModelPath, "changed");
        await Generate(service, Settings);
        Assert.False(requests.Last().Options.PerformanceBoost);
    }

    [Fact]
    public async Task Calibration_AwaitsWorkerExitBeforeSamplingNextMemoryBudget()
    {
        using var service = Create((_, _) =>
        {
            Assert.DoesNotContain(workers, worker => !worker.IsAlive && !worker.ExitWaited);
            return Task.CompletedTask;
        });
        await service.TestPerformanceAsync(Settings, CancellationToken.None);
        Assert.All(workers, worker => Assert.True(worker.ExitWaited));
    }

    [Fact]
    public async Task PermissionWithoutMeasuredProfile_DoesNotActivateResourceLimitsForOrdinaryGeneration()
    {
        using var service = Create();
        await Generate(service, Settings);
        Assert.All(requests, request => Assert.False(request.ProtectResources || request.Options.PerformanceBoost));
        Assert.True(service.IsLoaded);
    }

    [Theory]
    [InlineData(AiMemoryMode.Automatic)]
    [InlineData(AiMemoryMode.Manual)]
    public async Task UnavailableBoostLimitsDuringProbe_FallBackToOrdinaryGeneration(AiMemoryMode mode)
    {
        var settings = Settings with { MemoryMode = mode, GpuLayers = 4 };
        using var service = Create((request, _) =>
        {
            if(request.Command == "probe" && request.Options.PerformanceBoost) throw new AiException("AI.ResourceGuardUnavailable");
            return Task.CompletedTask;
        });
        await service.TestPerformanceAsync(settings, CancellationToken.None);
        await Generate(service, settings);
        Assert.False(requests.Last().Options.PerformanceBoost);
        Assert.True(service.IsLoaded);
        Assert.Equal(settings.ContextLength, service.RuntimeOptions!.ContextLength);
        Assert.Equal(4, service.RuntimeOptions.GpuLayers);
        Assert.Contains(service.Notices, notice => notice.ResourceKey == "AI.PerformanceFallback");
    }

    [Fact]
    public async Task UnavailableBoostLimitsBeforeOutput_FallBackWithoutRepeatingDeliveredText()
    {
        using var service = Create((request, _) =>
        {
            if(request.Command == "generate" && request.Options.PerformanceBoost) throw new AiException("AI.ResourceGuardUnavailable");
            return Task.CompletedTask;
        });
        await service.TestPerformanceAsync(Settings, CancellationToken.None);
        await Generate(service, Settings);
        Assert.Equal(2, requests.Count(request => request.Command == "generate"));
        Assert.False(requests.Last().Options.PerformanceBoost);
        Assert.True(service.IsLoaded);
        Assert.Contains(service.Notices, notice => notice.ResourceKey == "AI.PerformanceFallback");
    }

    [Fact]
    public async Task Cancellation_PreservesPreviousResult_AndStopsOnlyOwnedWorkers()
    {
        using var service = Create();
        await service.TestPerformanceAsync(Settings, CancellationToken.None);
        string previous = File.ReadAllText(Path.Combine(directory, "profiles.json"));
        using var cancel = new CancellationTokenSource();
        using var interrupted = Create((request, _) =>
        {
            if(request.Command == "benchmark") cancel.Cancel();
            return Task.CompletedTask;
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrupted.TestPerformanceAsync(Settings, cancel.Token));
        Assert.Equal(previous, File.ReadAllText(Path.Combine(directory, "profiles.json")));
        Assert.All(workers, worker => Assert.False(worker.IsAlive));
        Assert.False(interrupted.IsBusy);
    }

    [Fact]
    public async Task Unload_CancelsTestWithoutReportingTimeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = Create(async (request, token) =>
        {
            if(request.Command != "benchmark") return;
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });
        var test = service.TestPerformanceAsync(Settings, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await service.UnloadAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test);
        Assert.False(service.IsBusy);
        Assert.False(File.Exists(Path.Combine(directory, "profiles.json")));
    }

    [Fact]
    public async Task PressureDuringTest_AbortsAndPreservesPreviousProfile()
    {
        File.WriteAllText(Path.Combine(directory, "profiles.json"), "[]");
        using var service = Create((request, _) =>
        {
            if(request.Command == "benchmark" && request.Options.PerformanceBoost) throw new AiException("AI.ResourcePressure");
            return Task.CompletedTask;
        });
        var error = await Assert.ThrowsAsync<AiException>(() => service.TestPerformanceAsync(Settings, CancellationToken.None));
        Assert.Equal("AI.ResourcePressure", error.ResourceKey);
        Assert.Equal("[]", File.ReadAllText(Path.Combine(directory, "profiles.json")));
        Assert.All(workers, worker => Assert.False(worker.IsAlive));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(128)]
    public void Candidates_KeepGpuAndContextBounded_AndReserveCpuCapacity(int processors)
    {
        var baseline = new AiRuntimeOptions(8192, 12);
        var candidates = AiPerformancePolicy.Candidates(baseline, processors);
        Assert.InRange(candidates.Count, 2, 4);
        Assert.False(candidates[0].PerformanceBoost);
        Assert.All(candidates, candidate =>
        {
            Assert.True(candidate.IsValid);
            Assert.Equal(baseline.ContextLength, candidate.ContextLength);
            Assert.Equal(baseline.GpuLayers, candidate.GpuLayers);
            Assert.Equal(baseline.BatchSize, candidate.BatchSize);
            Assert.InRange(candidate.Threads, 1, Math.Max(1, processors - 1));
        });
    }

    [Fact]
    public void ResourceBudget_ReservesPhysicalAndCommittedMemory()
    {
        Assert.Equal(2 * GiB, resources.Reserve);
        Assert.Equal(10 * GiB, resources.CommitLimit);
        Assert.Equal(13 * GiB, resources.ProcessCommitLimit(3 * GiB));
        Assert.Equal(0, (resources with { AvailableCommit = GiB }).ProcessCommitLimit(3 * GiB));
        Assert.False((resources with { AvailableCommit = GiB }).HasHeadroom);
        Assert.False(default(AiResourceSnapshot).HasHeadroom);
        Assert.Equal(0, (resources with { AvailableRam = GiB }).CommitLimit);
    }

    [Fact]
    public void InvalidPerformanceCache_DoesNotBreakNormalProfileSaving()
    {
        string key = AdaptiveAiInferenceService.PerformanceKey(Settings, Inspection.HardwareKey);
        File.WriteAllText(Path.Combine(directory, "profiles.json"), "[{\"Key\":\"" + key +
            "\",\"ModelPath\":\"model.gguf\",\"Options\":{\"ContextLength\":4096,\"GpuLayers\":4}," +
            "\"Performance\":{\"Baseline\":null,\"Selected\":null}}]");
        Assert.Null(Store.Find(key)!.Performance);
        Store.Save(new("normal", Settings.ModelPath, new(4096, 4), DateTime.UtcNow));
        Assert.NotNull(Store.Find("normal"));
    }

    private sealed class Worker(Func<AiWorkerRequest, CancellationToken, Task> execute,
        Func<AiWorkerRequest, AiPerformanceMeasurement> measure) : IAiWorkerSession
    {
        public bool IsAlive { get; private set; } = true;
        public bool ExitWaited { get; private set; }
        public async Task<AiModelInspection?> ExecuteAsync(AiWorkerRequest request, Action<string> onText,
            IProgress<float>? progress, CancellationToken token, Action<string>? onReasoning = null,
            Action<AiTokenUsage>? onUsage = null, Action<AiPerformanceMeasurement>? onMeasurement = null)
        {
            await execute(request, token);
            token.ThrowIfCancellationRequested();
            if(request.Command == "inspect") return Inspection;
            if(request.Command == "benchmark") onMeasurement?.Invoke(measure(request));
            if(request.Command == "generate") onText("ready");
            return null;
        }
        public void Stop() => IsAlive = false;
        public async Task StopAsync()
        {
            Stop();
            await Task.Yield(); // Model process teardown is asynchronous even after Kill returns.
            ExitWaited = true;
        }
        public void Dispose() => Stop();
    }
    public void Dispose() => Directory.Delete(directory, true);
}
